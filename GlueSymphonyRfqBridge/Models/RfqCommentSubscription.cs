// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

namespace GlueSymphonyRfqBridge.Models
{
    // Subscription to T42.RFQ.Comments
    public class RfqCommentSubscription
    {
        // the party which subscribes
        public string PartyName { get; private set; }

        public RfqCommentSubscription(string partyName)
        {
            PartyName = partyName;
        }
    }
}
