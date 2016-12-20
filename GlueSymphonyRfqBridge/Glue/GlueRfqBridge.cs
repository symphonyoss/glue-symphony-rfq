// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Collections.Generic;
using DOT.Logging;
using GlueSymphonyRfqBridge.Models;

namespace GlueSymphonyRfqBridge.Glue
{
    public class GlueRfqBridge : IGlueRfqBridge
    {
        private static readonly ISmartLogger Logger = new SmartLogger(typeof(GlueRfqBridge));

        private readonly object mx_ = new object();

        // counterParty
        private readonly HashSet<string> counterToRequestPartyCommentSubscriptions_ =
            new HashSet<string>();
        private readonly HashSet<string> quoteRequestSubscriptions_ =
            new HashSet<string>();
        // requestParty
        private readonly HashSet<string> requestToCounterPartyCommentSubscriptions_ =
            new HashSet<string>();

        // NB: we're not keeping a set of request subscriptions because
        //      for the moment we can't figure out when a request has 
        //      ended so we can delete it from the set; once we start
        //      tracking expirations, etc. we'll be able to do it
        // Besides, this is a loopback bridge for testing purposes

        public string Name
        {
            get
            {
                return "Glue";
            }
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public event EventHandler<Event<RfqComment>> CounterPartyCommentReceived;
        public event EventHandler<Event<RfqQuoteInquiryResponse>> QuoteInquiryResponseReceived;
        public event EventHandler<Event<RfqQuoteRequest>> QuoteRequestReceived;
        public event EventHandler<Event<RfqComment>> RequestPartyCommentReceived;

        public void SendCommentToCounterParty(RfqComment comment)
        {
            Logger.InfoFormat("Sending comment {0}", comment);

            lock (mx_)
            {
                if (!counterToRequestPartyCommentSubscriptions_.Contains(comment.CounterParty))
                {
                    // TODO: log we're ignoring
                    return;
                }
            }

            RaiseRequestPartyCommentReceived(comment);
        }

        public void SendCommentToRequestParty(RfqComment comment)
        {
            Logger.InfoFormat("Sending comment {0}", comment);

            lock (mx_)
            {
                if (!requestToCounterPartyCommentSubscriptions_.Contains(comment.RequestParty))
                {
                    // TODO: log we're ignoring
                    return;
                }
            }

            RaiseCounterPartyCommentReceived(comment);
        }

        public void SendQuoteInquiry(RfqQuoteInquiry quoteInquiry)
        {
            Logger.InfoFormat("Sending quote inquiry {0}", quoteInquiry);

            var handler = QuoteRequestReceived;
            if (handler == null)
            {
                return;
            }

            foreach (var counterParty in quoteInquiry.CounterParties)
            {
                handler(this, new Event<RfqQuoteRequest>(
                    new RfqQuoteRequest(
                        quoteInquiry.RequestId,
                        quoteInquiry.RequestParty,
                        counterParty,
                        quoteInquiry.ProductName,
                        quoteInquiry.ProductDetails,
                        quoteInquiry.Quantity,
                        quoteInquiry.RequestExpirationDate)));
            }
        }

        private void RaiseQuoteInquiryResponseReceived(RfqQuoteInquiryResponse response)
        {
            Logger.InfoFormat("Quote inquiry response received: {0}", response);

            var handler = QuoteInquiryResponseReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqQuoteInquiryResponse>(response));
            }
        }

        public void SubscribeForRequestPartyComments(string counterParty)
        {
            Logger.InfoFormat("Subscribing counter party {0} for request party comments", counterParty);

            lock (mx_)
            {
                counterToRequestPartyCommentSubscriptions_.Add(counterParty);
            }
        }

        private void RaiseRequestPartyCommentReceived(RfqComment comment)
        {
            Logger.InfoFormat("Request party comment received: {0}", comment);

            var handler = RequestPartyCommentReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqComment>(comment));
            }
        }

        public void UnsubscribeForRequestPartyComments(string counterParty)
        {
            Logger.InfoFormat("Unsubscribing counter party {0} for request party comments", counterParty);

            lock (mx_)
            {
                counterToRequestPartyCommentSubscriptions_.Remove(counterParty);
            }
        }

        public void SubscribeForCounterPartyComments(string requestParty)
        {
            Logger.InfoFormat("Subscribing request party {0} for counter party comments", requestParty);

            lock (mx_)
            {
                requestToCounterPartyCommentSubscriptions_.Add(requestParty);
            }
        }

        private void RaiseCounterPartyCommentReceived(RfqComment comment)
        {
            Logger.InfoFormat("Counter party comment received: {0}", comment);

            var handler = CounterPartyCommentReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqComment>(comment));
            }
        }

        public void UnsubscribeForCounterPartyComments(string requestParty)
        {
            Logger.InfoFormat("Unsubscribing request party {0} for counter party comments", requestParty);

            lock (mx_)
            {
                requestToCounterPartyCommentSubscriptions_.Remove(requestParty);
            }
        }

        public void SubscribeForQuoteRequests(string counterParty)
        {
            Logger.InfoFormat("Subscribing counter party {0} for request party RFQ requests", counterParty);

            lock (mx_)
            {
                quoteRequestSubscriptions_.Add(counterParty);
            }
        }

        private void RaiseQuoteRequestReceived(RfqQuoteRequest quoteRequest)
        {
            Logger.InfoFormat("Quote request received: {0}", quoteRequest);

            lock (mx_)
            {
                if (!quoteRequestSubscriptions_.Contains(quoteRequest.CounterParty))
                {
                    return;
                }
            }

            var handler = QuoteRequestReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqQuoteRequest>(quoteRequest));
            }
        }

        public void UnsubscribeForQuoteRequests(string counterParty)
        {
            Logger.InfoFormat("Unsubscribing counter party {0} for request party RFQ requests", counterParty);

            lock (mx_)
            {
                quoteRequestSubscriptions_.Remove(counterParty);
            }
        }

        public void SendQuoteInquiryResponse(RfqQuoteInquiryResponse quoteInquiryResponse)
        {
            Logger.InfoFormat("Sending quote inquiry response {0}", quoteInquiryResponse);

            RaiseQuoteInquiryResponseReceived(quoteInquiryResponse);
        }
    }
}
