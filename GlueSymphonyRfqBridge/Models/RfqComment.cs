// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

namespace GlueSymphonyRfqBridge.Models
{
    public class RfqComment
    {
        public string RequestId { get; private set; }
        public string RequestParty { get; private set; }
        public string CounterParty { get; private set; }
        public string ProductName { get; private set; }
        public string Comment { get; private set; }

        public RfqComment(
            string requestId,
            string requestParty,
            string counterParty,
            string productName,
            string comment)
        {
            RequestId = requestId;
            RequestParty = requestParty;
            CounterParty = counterParty;
            ProductName = productName;
            Comment = comment;
        }

        public override string ToString()
        {
            return string.Format("[RfqComment: RequestId={0}, RequestParty={1}, CounterParty={2}, ProductName={3}, Comment={4}]", RequestId, RequestParty, CounterParty, ProductName, Comment);
        }
    }
}
