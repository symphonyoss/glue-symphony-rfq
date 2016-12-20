// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOT.AGM;
using DOT.AGM.Server;
using DOT.Logging;
using GlueSymphonyRfqBridge.Glue.Extensions;
using GlueSymphonyRfqBridge.Models;

namespace GlueSymphonyRfqBridge.Glue
{
    // TODO: use AGM stream branching for party-to-party comments in the next iteration
    public class GlueRfqServer : IGlueRfqServer
    {
        private static ISmartLogger Logger = new SmartLogger(typeof(GlueRfqServer));

        private IGlueRfqBridge bridge_;

        private IServer server_;
        private IServerEventStream quoteRequestStream_;
        private IServerEventStream quoteInquiryStream_;
        private IServerEventStream counterPartyCommentStream_;
        private IServerEventStream requestPartyCommentStream_;

        private long nextRequestId_ = 0;

        private readonly Dictionary<string, QuoteInquirySubscription> requestsById_ =
            new Dictionary<string, QuoteInquirySubscription>();
        private readonly Dictionary<IEventStreamSubscriber, string> requestsBySubscriber_ =
            new Dictionary<IEventStreamSubscriber, string>();

        private readonly Dictionary<string, HashSet<IEventStreamSubscriber>> counterToRequestPartyCommentSubscriptions_ =
            new Dictionary<string, HashSet<IEventStreamSubscriber>>();
        private readonly Dictionary<string, HashSet<IEventStreamSubscriber>> requestToCounterPartyCommentSubscriptions_ =
            new Dictionary<string, HashSet<IEventStreamSubscriber>>();

        private readonly Dictionary<string, IEventStreamSubscriber> counterPartyRequestSubscribers_ =
            new Dictionary<string, IEventStreamSubscriber>();

        private readonly object mx_ = new object();

        public GlueRfqServer(IServer server)
        {
            server_ = server;
        }

        public void Start(IGlueRfqBridge bridge)
        {
            Logger.Info("Starting GLUE RFQ server...");

            bridge_ = bridge;

            // on "local" side
            bridge_.QuoteInquiryResponseReceived += OnQuoteInquiryResponseReceived;
            bridge_.CounterPartyCommentReceived += OnCounterPartyCommentReceived;

            // on "remote" side
            bridge_.QuoteRequestReceived += OnQuoteRequestReceived;
            bridge_.RequestPartyCommentReceived += OnRequestPartyCommentReceived;

            RegisterMethodsAndStreams();

            Logger.Info("GLUE RFQ server started.");
        }

        public void Stop()
        {
            Logger.Info("Stopping GLUE RFQ server...");
            
            bridge_.QuoteInquiryResponseReceived -= OnQuoteInquiryResponseReceived;
            bridge_.CounterPartyCommentReceived -= OnCounterPartyCommentReceived;
            bridge_.QuoteRequestReceived -= OnQuoteRequestReceived;
            bridge_.RequestPartyCommentReceived -= OnRequestPartyCommentReceived;

            Logger.Info("GLUE RFQ server stopped.");
        }

        private void RegisterMethodsAndStreams()
        {
            //-- LOCAL side

            // LOCAL side GLUE requestor client subscribes on T42.RFQ.QuoteInquiryStream
            // Server calls Bridge to send inquiry over chat to REMOTE side
            // When Bridge raises QuoteInquiryResponseReceived because
            //		a response was received on chat or (maybe because REMOTE side
            //			called T42.RFQ.SendQuoteResponse),
            //		Server pushes responses to this stream (T42.RFQ.QuoteInquiryStream)
            var quoteInquiryStreamMethod = server_.CreateEventStreamingMethod(
                mb => mb.SetMethodName("T42.RFQ.QuoteInquiryStream")
                    .SetDescription("")
                    .SetDisplayName("Quote Stream")
                    .SetParameterSignature(@"
                        string requestParty,
                        string[] counterParties,
                        string productName,
                        double quantity,
                        DateTime requestExpirationDate
                    ")
                    .SetResultSignature(@"
                        string responseType,
                        string requestId,
                        string? responseMessage,
                        string? counterParty,
                        string? productName,
                        double? quantity,
                        double? quotePrice
                    "),
                new GlueStreamHandler(
                    OnQuoteInquirySubscription,
                    OnQuoteInquirySubscriberAdded,
                    OnQuoteInquirySubscriberRemoved),
                out quoteInquiryStream_);

            // LOCAL side GLUE requestor client posts comment on T42.RFQ.SendCommentToCounterParty
            // Server calls Bridge to send comment over chat to REMOTE side
            // When REMOTE side publishes a comment back via chat (maybe by calling 
            //		T42.RFQ.SendCommentToRequestParty first), 
            //		Server pushesh comment to LOCAL side on T42.RFQ.CounterPartyCommentStream
            var sendCommentToCounterPartyMethod = server_.CreateAsyncMethod(
                mb => mb
                    .SetMethodName("T42.RFQ.SendCommentToCounterParty")
                    .SetDescription("")
                    .SetDisplayName("Send Comment to Counter Party")
                    .SetParameterSignature(@"
                        string? requestId,
                        string requestParty,
                        string counterParty,
                        string? productName,
                        string comment
                    ")
                    .SetResultSignature(""),
                SendCommentToCounterPartyMethodHandler);

            // REMOTE side sends a comment over chat (maybe a GLUE client on that side
            //		called T42.RFQ.SendCommentToRequestParty)
            //	Server pushes comment to LOCAL side's T42.RFQ.CounterPartyCommentStream
            var counterPartyCommentStreamMethod = server_.CreateEventStreamingMethod(
                mb => mb.SetMethodName("T42.RFQ.CounterPartyCommentStream")
                    .SetDescription("")
                    .SetDisplayName("Counter Party Comments Stream")
                    .SetParameterSignature("string requestParty")
                    .SetResultSignature(@"
                        string? requestId,
                        string requestParty,
                        string counterParty,
                        string? productName,
                        string comment
                    "),
                new GlueStreamHandler(
                    OnRequestToCounterPartyCommentSubscription,
                    OnRequestToCounterPartyCommentSubscriberAdded,
                    OnRequestToCounterPartyCommentSubscriberRemoved),
                out counterPartyCommentStream_);

            //-- REMOTE side

            // When the REMOTE side receives a quote request over chat (maybe from 
            //		LOCAL side subscribing to T42.RFQ.QuoteInquiryStream),
            //		REMOTE side calls T42.RFQ.SendQuoteResponse to send a 
            //		response back.
            var sendQuoteResponseMethod = server_.CreateAsyncMethod(
                mb => mb
                    .SetMethodName("T42.RFQ.SendQuoteResponse")
                    .SetDescription("")
                    .SetDisplayName("Send Quote")
                    .SetParameterSignature(@"
                        string requestId,
                        string requestParty,
                        string? responseMessage,
                        string counterParty,
                        string? productName,
                        double? quantity,
                        double price
                    ")
                    .SetResultSignature(""),
                SendQuoteResponseMethodHandler);

            // When the REMOTE side receives a comment from LOCAL side over chat
            //		(maybe by LOCAL side calling T42.RFQ.SendCommentToCounterParty)
            //	Server sends comment using Bridge, and 
            //		on the REMOTE side Server pushes comment to T42.RFQ.RequestPartyCommentStream
            //	When the REMOTE side replies back by calling T42.RFQ.SendCommentToRequestParty
            //		Server calls Bridge to send comment back over the chat and
            //		on the LOCAL side pushes comment to T42.RFQ.CounterPartyCommentStream
            var sendCommentToRequestPartyMethod = server_.CreateAsyncMethod(
                mb => mb
                    .SetMethodName("T42.RFQ.SendCommentToRequestParty")
                    .SetDescription("")
                    .SetDisplayName("Send Comment to Request Party")
                    .SetParameterSignature(@"
                        string? requestId,
                        string requestParty,
                        string counterParty,
                        string? productName,
                        string comment
                    ")
                    .SetResultSignature(""),
                SendCommentToRequestPartyMethodHandler);

            // When the LOCAL side sends a quote request (possibly by subscribing to
            //		T42.RFQ.QuoteInquiryStream), Server calls Bridge to send
            //		request over the chat.
            //	On the REMOTE side, Server pushes request to T42.RFQ.QuoteRequestStream
            //	REMOTE side can then reply by calling T42.RFQ.SendQuoteResponse
            //		and after responses arrives on chat, Server will push the response
            //		to the LOCAL side's T42.RFQ.QuoteInquiryStream
            var quoteRequestStreamMethod = server_.CreateEventStreamingMethod(
                mb => mb.SetMethodName("T42.RFQ.QuoteRequestStream")
                    .SetDescription("")
                    .SetDisplayName("RFQ Requests Stream")
                    .SetParameterSignature("string counterParty")
                    .SetResultSignature(@"
                        string requestParty,
                        string requestId,
                        string counterParty,
                        string productName,
                        double quantity,
                        DateTime requestExpirationDate
                    "),
                new GlueStreamHandler(
                    OnQuoteRequestSubscription,
                    OnQuoteRequestSubscriberAdded,
                    OnQuoteRequestSubscriberRemoved),
                out quoteRequestStream_);

            // LOCAL side sends comment (maybe by calling T42.RFQ.SendCommentToCounterParty)
            // On the REMOTE side, Server pushesh comment to T42.RFQ.RequestPartyCommentStream
            //		where the REMOTE side can later reply by calling T42.RFQ.SendCommentToRequestParty
            //		and the LOCAL side will get that on the T42.RFQ.CounterPartyCommentStream
            var requestPartyCommentStreamMethod = server_.CreateEventStreamingMethod(
                mb => mb.SetMethodName("T42.RFQ.RequestPartyCommentStream")
                    .SetDescription("")
                    .SetDisplayName("Request Party Comments Stream")
                    .SetParameterSignature("string counterParty")
                    .SetResultSignature(@"
                        string requestParty,
                        string counterParty,
                        string productName,
                        double quantity,
                        DateTime requestExpirationDate
                    "),
                new GlueStreamHandler(
                    OnCounterToRequestPartyCommentSubscription,
                    OnCounterToRequestPartyCommentSubscriberAdded,
                    OnCounterToRequestPartyCommentSubscriberRemoved),
                out requestPartyCommentStream_);

            // register all methods & streams en masse to avoid races

            server_.RegisterMethods(
                // LOCAL side
                quoteInquiryStreamMethod,
                sendCommentToCounterPartyMethod,
                counterPartyCommentStreamMethod,
                // REMOTE side
                sendQuoteResponseMethod,
                sendCommentToRequestPartyMethod,
                quoteRequestStreamMethod,
                requestPartyCommentStreamMethod);
        }

        private string GenerateRequestId()
        {
            return Interlocked.Increment(ref nextRequestId_).ToString();
        }

        // LOCAL side GLUE requestor client subscribes on T42.RFQ.QuoteInquiryStream
        // Server calls Bridge to send inquiry over chat to REMOTE side
        // When Bridge raises QuoteInquiryResponseReceived because
        //		a response was received on chat or (maybe because REMOTE side
        //			called T42.RFQ.SendQuoteResponse),
        //		Server pushes responses to this stream (T42.RFQ.QuoteInquiryStream)

        private bool OnQuoteInquirySubscription(IEventStreamSubscriptionRequest subscription, ref string rejectMessage)
        {
            // TODO: validate request
            return true;
        }

        // NB: all handlers intentionally don't trap any errors, let AGM handle these

        private void OnQuoteInquirySubscriberAdded(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var requestParty = args["requestParty"].Value.AsString;
            var counterParties = args["counterParties"].Value.AsStringArray;
            var productName = args["productName"].Value.AsString;
            var quantity = args.SafeGetDouble("quantity");
            var requestExpirationDate = GetDate(args["requestExpirationDate"].Value);
            var requestId = GenerateRequestId();

            var quoteInquiry = new RfqQuoteInquiry(
                    requestId,
                    requestParty,
                    productName,
                    null,
                    quantity,
                    requestExpirationDate,
                counterParties);

            lock (mx_)
            {
                // track request
                requestsById_.Add(requestId, new QuoteInquirySubscription(quoteInquiry, subscriber));
                requestsBySubscriber_.Add(subscriber, requestId);
                // do the actual work
                try
                {
                    // push SetRequestId(requestId) back to subscriber
                    subscriber.Push(cb =>
                    {
                        cb.AddValue("responseType", RfqResponseType.SetRequestId.ToString());
                        cb.AddValue("requestId", requestId);
                    });

                    // send request via the bridge
                    bridge_.SendQuoteInquiry(quoteInquiry);
                }
                catch (Exception e)
                {
                    // undo tracking
                    requestsById_.Remove(requestId);
                    requestsBySubscriber_.Remove(subscriber);

                    Logger.Error(string.Format("Failed to send {0}", quoteInquiry), e);

                    subscriber.Push(cb =>
                    {
                        cb.AddValue("responseType", RfqResponseType.Error.ToString());
                        cb.AddValue("requestId", requestId);
                        cb.AddValue("responseMessage", string.Format("Unexpected error: {0}", e.Message));
                    });
                }
            }
        }

        private DateTime GetDate(Value value)
        {
            if (value.Type == AgmValueType.DateTime)
            {
                return value.AsDateTime;
            }
            if (value.Type == AgmValueType.String)
            {
                var result = DateTime.Parse(value.AsString);
                return result;
            }
            throw new NotSupportedException();
        }

        private void OnQuoteInquirySubscriberRemoved(IEventStreamSubscriber subscriber)
        {
            lock (mx_)
            {
                var requestId = requestsBySubscriber_[subscriber];
                requestsById_.Remove(requestId);
                requestsBySubscriber_.Remove(subscriber);
            }
        }

        // REMOTE side sends a comment over chat (maybe a GLUE client on that side
        //		called T42.RFQ.SendCommentToRequestParty)
        //	Server pushes comment to LOCAL side's T42.RFQ.CounterPartyCommentStream

        private bool OnRequestToCounterPartyCommentSubscription(IEventStreamSubscriptionRequest subscription, ref string rejectMessage)
        {
            // TODO: validate request
            return true;
        }

        private void OnRequestToCounterPartyCommentSubscriberAdded(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var requestParty = args["requestParty"].Value.AsString;

            lock (mx_)
            {
                HashSet<IEventStreamSubscriber> subscribers;
                if (!requestToCounterPartyCommentSubscriptions_.TryGetValue(
                    requestParty,
                    out subscribers))
                {
                    subscribers = new HashSet<IEventStreamSubscriber>();
                    requestToCounterPartyCommentSubscriptions_.Add(
                        requestParty,
                        subscribers);
                }
                subscribers.Add(subscriber);
            }

            bridge_.SubscribeForCounterPartyComments(requestParty);
        }

        private void OnRequestToCounterPartyCommentSubscriberRemoved(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var requestParty = args["requestParty"].Value.AsString;

            var unsubscribe = false;
            lock (mx_)
            {
                HashSet<IEventStreamSubscriber> subscribers;
                if (requestToCounterPartyCommentSubscriptions_.TryGetValue(
                    requestParty,
                    out subscribers))
                {
                    unsubscribe = true;
                    subscribers.Remove(subscriber);
                    if (subscribers.Count == 0)
                    {
                        requestToCounterPartyCommentSubscriptions_.Remove(requestParty);
                    }
                }
            }

            if (unsubscribe)
            {
                bridge_.UnsubscribeForCounterPartyComments(requestParty);
            }
        }

        // When the LOCAL side sends a quote request (possibly by subscribing to
        //		T42.RFQ.QuoteInquiryStream), Server calls Bridge to send
        //		request over the chat.
        //	On the REMOTE side, Server pushes request to T42.RFQ.QuoteRequestStream
        //	REMOTE side can then reply by calling T42.RFQ.SendQuoteResponse
        //		and after responses arrives on chat, Server will push the response
        //		to the LOCAL side's T42.RFQ.QuoteInquiryStream

        private bool OnQuoteRequestSubscription(IEventStreamSubscriptionRequest subscription, ref string rejectMessage)
        {
            // TODO: validate request
            return true;
        }

        private void OnQuoteRequestSubscriberAdded(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var counterParty = args["counterParty"].Value.AsString;

            Logger.InfoFormat("Adding quote request subscriber for counter party {0} - {1}",
                counterParty,
                subscriber.Subscription.Caller);

            lock (mx_)
            {
                counterPartyRequestSubscribers_.Add(counterParty, subscriber);
            }
            bridge_.SubscribeForQuoteRequests(counterParty);
        }

        private void OnQuoteRequestSubscriberRemoved(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var counterParty = args["counterParty"].Value.AsString;

            Logger.InfoFormat("Removing quote request subscriber for counter party {0} - {1}",
                counterParty,
                subscriber.Subscription.Caller);

            bool unsubscribe;
            lock (mx_)
            {
                unsubscribe = counterPartyRequestSubscribers_.Remove(counterParty);
            }
            if (unsubscribe)
            {
                bridge_.UnsubscribeForQuoteRequests(counterParty);
            }
        }

        // LOCAL side sends comment (maybe by calling T42.RFQ.SendCommentToCounterParty)
        // On the REMOTE side, Server pushesh comment to T42.RFQ.RequestPartyCommentStream
        //		where the REMOTE side can later reply by calling T42.RFQ.SendCommentToRequestParty
        //		and the LOCAL side will get that on the T42.RFQ.CounterPartyCommentStream

        private bool OnCounterToRequestPartyCommentSubscription(IEventStreamSubscriptionRequest subscription, ref string rejectMessage)
        {
            // TODO: validate request
            return true;
        }

        private void OnCounterToRequestPartyCommentSubscriberAdded(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var counterParty = args["counterParty"].Value.AsString;

            lock (mx_)
            {
                HashSet<IEventStreamSubscriber> subscribers;
                if (!counterToRequestPartyCommentSubscriptions_.TryGetValue(
                    counterParty,
                    out subscribers))
                {
                    subscribers = new HashSet<IEventStreamSubscriber>();
                    counterToRequestPartyCommentSubscriptions_.Add(
                        counterParty,
                        subscribers);
                }
                subscribers.Add(subscriber);
            }

            bridge_.SubscribeForRequestPartyComments(counterParty);
        }

        private void OnCounterToRequestPartyCommentSubscriberRemoved(IEventStreamSubscriber subscriber)
        {
            var args = subscriber.Subscription.SubscriptionContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);
            var counterParty = args["counterParty"].Value.AsString;

            var unsubscribe = false;
            lock (mx_)
            {
                HashSet<IEventStreamSubscriber> subscribers;
                if (counterToRequestPartyCommentSubscriptions_.TryGetValue(
                    counterParty,
                    out subscribers))
                {
                    unsubscribe = true;
                    subscribers.Remove(subscriber);
                    if (subscribers.Count == 0)
                    {
                        counterToRequestPartyCommentSubscriptions_.Remove(counterParty);
                    }
                }
            }

            if (unsubscribe)
            {
                bridge_.UnsubscribeForRequestPartyComments(counterParty);
            }
        }

        // When the REMOTE side receives a quote request over chat (maybe from 
        //		LOCAL side subscribing to T42.RFQ.QuoteInquiryStream),
        //		REMOTE side calls T42.RFQ.SendQuoteResponse to send a 
        //		response back.
        // Server calls Bridge to send response over chat

        private void SendQuoteResponseMethodHandler(
            IServerMethod method,
            IMethodInvocationContext invocationContext,
            IInstance caller,
            IServerMethodResultBuilder resultBuilder,
            Action<IServerMethodResult> asyncResponseCallback,
            object cookie)
        {
            var args = invocationContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);

            var quantity = args.ContainsKey("quantity") ? (double?)args.SafeGetDouble("quantity") : null;

            bridge_.SendQuoteInquiryResponse(
                new RfqQuoteInquiryResponse(
                    RfqResponseType.Quote,
                    args.TryGetString("responseMessage"),
                    args["requestId"].Value.AsString,
                    args["requestParty"].Value.AsString,
                    args["counterParty"].Value.AsString,
                    args.TryGetString("productName"),
                    null,
                    quantity,
                    args.SafeGetDouble("price")));
            
            asyncResponseCallback(resultBuilder.Build());
        }

        // LOCAL side GLUE requestor client posts comment on T42.RFQ.SendCommentToCounterParty
        // Server calls Bridge to send comment over chat to REMOTE side
        // When REMOTE side publishes a comment back via chat (maybe by calling 
        //		T42.RFQ.SendCommentToRequestParty first), 
        //		Server pushesh comment to LOCAL side on T42.RFQ.CounterPartyCommentStream

        private void SendCommentToCounterPartyMethodHandler(
            IServerMethod method,
            IMethodInvocationContext invocationContext,
            IInstance caller,
            IServerMethodResultBuilder resultBuilder,
            Action<IServerMethodResult> asyncResponseCallback,
            object cookie)
        {
            var args = invocationContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);

            bridge_.SendCommentToCounterParty(
                new RfqComment(
                    args.TryGetString("requestId"),
                    args["requestParty"].Value.AsString,
                    args["counterParty"].Value.AsString,
                    args.TryGetString("productName"),
                    args["comment"].Value.AsString));
            
            asyncResponseCallback(resultBuilder.Build());
        }

        // When the REMOTE side receives a comment from LOCAL side over chat
        //		(maybe by LOCAL side calling T42.RFQ.SendCommentToCounterParty)
        //	Server sends comment using Bridge, and 
        //		on the REMOTE side Server pushes comment to T42.RFQ.RequestPartyCommentStream
        //	When the REMOTE side replies back by calling T42.RFQ.SendCommentToRequestParty
        //		Server calls Bridge to send comment back over the chat and
        //		on the LOCAL side pushes comment to T42.RFQ.CounterPartyCommentStream

        private void SendCommentToRequestPartyMethodHandler(
            IServerMethod method,
            IMethodInvocationContext invocationContext,
            IInstance caller,
            IServerMethodResultBuilder resultBuilder,
            Action<IServerMethodResult> asyncResponseCallback,
            object cookie)
        {
            var args = invocationContext.Arguments.ToDictionary(cv => cv.Name, cv => cv);

            bridge_.SendCommentToRequestParty(
                new RfqComment(
                    args.TryGetString("requestId"),
                    args["requestParty"].Value.AsString,
                    args["counterParty"].Value.AsString,
                    args.TryGetString("productName"),
                    args["comment"].Value.AsString));
            
            asyncResponseCallback(resultBuilder.Build());
        }

        // When Bridge raises QuoteInquiryResponseReceived because
        //		a response was received on chat or (maybe because REMOTE side
        //			called T42.RFQ.SendQuoteResponse),
        //		Server pushes responses to this stream (T42.RFQ.QuoteInquiryStream)

        private void OnQuoteInquiryResponseReceived(object sender, Event<RfqQuoteInquiryResponse> e)
        {
            Logger.InfoFormat("Inquiry response received {0}", e.Data);

            var response = e.Data;

            QuoteInquirySubscription request;
            lock (mx_)
            {
                if (!requestsById_.TryGetValue(response.RequestId, out request))
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Ignoring response, request not found on this end: {0}", response);
                    }
                    return;
                }
            }

            request.Subscriber.Push(cb =>
            {
                cb.AddValue("responseType", response.ResponseType.ToString());
                if (response.ResponseMessage != null)
                {
                    cb.AddValue("responseMessage", response.ResponseMessage);
                }
                cb.AddValue("requestId", response.RequestId);
                cb.AddValue("requestParty", response.RequestParty);
                cb.AddValue("counterParty", response.CounterParty);
                if (response.ProductName != null)
                {
                    cb.AddValue("productName", response.ProductName);
                }
                if (response.Quantity.HasValue)
                {
                    cb.AddValue("quantity", response.Quantity.Value);
                }
                if (response.Price.HasValue)
                {
                    cb.AddValue("price", response.Price.Value);
                }
            });
        }

        // Request sent by request party received by counter party
        // Stream it to REMOTE side on T42.RFQ.QuoteRequestStream

        private void OnQuoteRequestReceived(object sender, Event<RfqQuoteRequest> e)
        {
            Logger.InfoFormat("Quote request received {0}", e.Data);

            var request = e.Data;

            IEventStreamSubscriber subscriber;
            lock (mx_)
            {
                if (!counterPartyRequestSubscribers_.TryGetValue(request.CounterParty, out subscriber))
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Ignoring quote request, there's no counter party {0} on this end",
                                           request.CounterParty);
                    }
                    return;
                }
            }

            subscriber.Push(cb =>
            {
                cb.AddValue("requestId", request.RequestId);
                cb.AddValue("requestParty", request.RequestParty);
                cb.AddValue("counterParty", request.CounterParty);
                cb.AddValue("productName", request.ProductName);
                cb.AddValue("quantity", request.Quantity);
                cb.AddValue("requestExpirationDate", request.RequestExpirationDate);
            });
        }

        // Comment sent by counter party received by request party
        // Stream it on T42.RFQ.CounterPartyCommentStream

        private void OnCounterPartyCommentReceived(object sender, Event<RfqComment> e)
        {
            Logger.InfoFormat("Counter party comment received {0}", e.Data);

            PushComment(e.Data, comment => comment.RequestParty, requestToCounterPartyCommentSubscriptions_);
        }

        // Comment from request party received by counter party
        // Stream it to REMOTE side on T42.RFQ.RequestPartyCommentStream

        private void OnRequestPartyCommentReceived(object sender, Event<RfqComment> e)
        {
            Logger.InfoFormat("Request party comment received {0}", e.Data);

            PushComment(e.Data, comment => comment.CounterParty, counterToRequestPartyCommentSubscriptions_);
        }

        private void PushComment(
            RfqComment comment,
            Func<RfqComment, string> partyGetter,
            Dictionary<string, HashSet<IEventStreamSubscriber>> subscriptions)
        {
            lock (mx_)
            {
                HashSet<IEventStreamSubscriber> subscribers;
                if (subscriptions.TryGetValue(
                    partyGetter(comment),
                    out subscribers))
                {
                    foreach (var subscriber in subscribers)
                    {
                        subscriber.Push(cb => PopulateComment(cb, comment));
                    }
                }
            }
        }

        private static void PopulateComment(IContextBuilder cb, RfqComment comment)
        {
            if (comment.RequestId != null)
            {
                cb.AddValue("requestId", comment.RequestId);
            }
            cb.AddValue("requestParty", comment.RequestParty);
            cb.AddValue("counterParty", comment.CounterParty);
            if (comment.ProductName != null)
            {
                cb.AddValue("productName", comment.ProductName);
            }
            cb.AddValue("comment", comment.Comment);
        }

        private class QuoteInquirySubscription
        {
            public RfqQuoteInquiry QuoteInquiry { get; private set; }
            public IEventStreamSubscriber Subscriber { get; private set; }

            public QuoteInquirySubscription(
                RfqQuoteInquiry quoteInquiry,
                IEventStreamSubscriber subscriber)
            {
                Subscriber = subscriber;
                QuoteInquiry = quoteInquiry;
            }
        }
    }
}
