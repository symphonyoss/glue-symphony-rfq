// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;

namespace GlueSymphonyRfqBridge.Models
{
    public class RfqQuoteRequest
    {
        public string RequestId { get; private set; }
        public string RequestParty { get; private set; }
        public string CounterParty { get; private set; }
        public string ProductName { get; private set; }
        public ProductDetails ProductDetails { get; private set; }
        public double Quantity { get; private set; }
        public DateTime RequestExpirationDate { get; private set; }

        public RfqQuoteRequest(
            string requestId,
            string requestParty,
            string counterParty,
            string productName,
            ProductDetails productDetails,
            double quantity,
            DateTime requestExpirationDate)
        {
            RequestId = requestId;
            RequestParty = requestParty;
            CounterParty = counterParty;
            ProductName = productName;
            ProductDetails = productDetails;
            RequestExpirationDate = requestExpirationDate;
            Quantity = quantity;
        }

        public override string ToString()
        {
            return string.Format("[RfqQuoteRequest: RequestId={0}, RequestParty={1}, CounterParty={2}, ProductName={3}, ProductDetails={4}, Quantity={5}, RequestExpirationDate={6}]", RequestId, RequestParty, CounterParty, ProductName, ProductDetails, Quantity, RequestExpirationDate);
        }
    }
}
