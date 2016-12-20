// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using DOT.AGM.Server;

namespace GlueSymphonyRfqBridge.Glue
{
    public delegate bool AcceptSubscriptionDelegate(
        IEventStreamSubscriptionRequest subscriptionRequest,
        ref string message);

    public class GlueStreamHandler : IServerEventStreamHandler
    {
        private readonly AcceptSubscriptionDelegate acceptor_;
        private readonly Action<IEventStreamSubscriber> subscriberAdded_;
        private readonly Action<IEventStreamSubscriber> subscriberRemoved_;

        public GlueStreamHandler(
            AcceptSubscriptionDelegate acceptor,
            Action<IEventStreamSubscriber> subscriberAdded = null,
            Action<IEventStreamSubscriber> subscriberRemoved = null)
        {
            acceptor_ = acceptor;
            subscriberAdded_ = subscriberAdded;
            subscriberRemoved_ = subscriberRemoved;
        }

        public void HandleStreamBranchClosed(
            IServerEventStream serverEventStream,
            IEventStreamBranch branch,
            object streamCookie = null)
        {
        }

        public void HandleStreamClosed(
            IServerEventStream serverEventStream,
            object streamCookie = null)
        {
        }

        public void HandleSubscriber(
            IServerEventStream serverEventStream,
            IEventStreamSubscriber subscriber,
            IEventStreamBranch branch,
            object streamCookie = null)
        {
            if (subscriberAdded_ != null)
            {
                subscriberAdded_(subscriber);
            }
        }

        public void HandleSubscriberRemoved(
            IServerEventStream serverEventStream,
            IEventStreamSubscriber subscriber,
            EventStreamSubscriberRemovedContext subscriberRemovedContext,
            IEventStreamBranch branch = null,
            object streamCookie = null)
        {
            if (subscriberRemoved_ != null)
            {
                subscriberRemoved_(subscriber);
            }
        }

        public IEventStreamBranch HandleSubscriptionRequest(
            IServerEventStream serverEventStream,
            IEventStreamSubscriptionRequest subscriptionRequest,
            object streamCookie = null)
        {
            string message = null;

            try
            {
                if (!acceptor_(subscriptionRequest, ref message))
                {
                    subscriptionRequest.Reject(
                        reply => reply.SetIsFailed(true).SetMessage(message).Build());
                    return null;
                }
                return subscriptionRequest.Accept(
                    reply => reply.SetMessage(message ?? "Subscribed").Build());
            }
            catch (Exception e)
            {
                subscriptionRequest.Reject(
                    reply => reply.SetIsFailed(true)
                        .SetMessage(string.Format("Unexpected error: {0}", e.Message))
                        .Build());
                return null;
            }
        }
    }
}
