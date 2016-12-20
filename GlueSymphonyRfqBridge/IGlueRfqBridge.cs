// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using GlueSymphonyRfqBridge.Models;

namespace GlueSymphonyRfqBridge
{
    public interface IGlueRfqBridge
    {
        string Name { get; }

        void Start();
        void Stop();

        //-- "LOCAL" side

        // LOCAL side GLUE requestor client subscribes on T42.RFQ.QuoteInquiryStream
        // Bridge sends inquiry over chat to REMOTE side
        // REMOTE side receives both on chat and on T42.RFQ.QuoteRequestStream
        void SendQuoteInquiry(RfqQuoteInquiry quoteInquiry);

        // REMOTE side sends quote response over chat
        // LOCAL side GLUE requestor receives response as stream push on T42.RFQ.QuoteInquiryStream
        event EventHandler<Event<RfqQuoteInquiryResponse>> QuoteInquiryResponseReceived;

        // LOCAL side GLUE requestor client posts comment on T42.RFQ.SendCommentToCounterParty
        // Bridge sends comment over chat to REMOTE side
        void SendCommentToCounterParty(RfqComment comment);

        // REMOTE side sends comment over chat
        // LOCAL side GLUE requestor client receives comment as stream push on T42.RFQ.CounterPartyCommentStream
        event EventHandler<Event<RfqComment>> CounterPartyCommentReceived;

        //-- "REMOTE" side

        // LOCAL side sends quote request over chat to REMOTE side
        // REMOTE side counterparty GLUE client receives quote requests as stream push on T42.RFQ.QuoteRequestStream
        event EventHandler<Event<RfqQuoteRequest>> QuoteRequestReceived;

        // REMOTE side gets called T42.RFQ.SendQuoteResponse
        // REMOTE side sends quote response over chat
        // LOCAL side GLUE requestor client receives response on T42.RFQ.QuoteInquiryStream
        void SendQuoteInquiryResponse(RfqQuoteInquiryResponse quoteInquiryResponse);

        // LOCAL party sends request party comment over chat to REMOTE side
        // REMOTE side counterparty receives request party comment as stream push on T42.RFQ.RequestPartyCommentStream
        event EventHandler<Event<RfqComment>> RequestPartyCommentReceived;

        // REMOTE side posts comment to request part by calling T42.RFQ.SendCommentToRequestParty
        // Bridge sends comment to REMOTE side request party over char
        void SendCommentToRequestParty(RfqComment comment);

        // Called when the REMOTE party gets a subscription on T42.RFQ.RequestPartyCommentStream
        // 		so it can tell the bridge to start dispatching messages sent to this party
        void SubscribeForRequestPartyComments(string counterParty);
        void UnsubscribeForRequestPartyComments(string counterParty);

        // Called when the LOCAL party gets a subscription on T42.RFQ.CounterPartyCommentStream
        // 		so it can tell the bridge to start dispatching messages sent to the rquest party
        void SubscribeForCounterPartyComments(string requestParty);
        void UnsubscribeForCounterPartyComments(string requestParty);

        // Called when the REMOTE party gets a subscription on T42.RFQ.QuoteRequestStream
        // 		so it can tell the bridge to start listening to messages from this party
        void SubscribeForQuoteRequests(string counterParty);
        void UnsubscribeForQuoteRequests(string counterParty);
    }
}

