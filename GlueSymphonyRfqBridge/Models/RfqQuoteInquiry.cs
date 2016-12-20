// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;

namespace GlueSymphonyRfqBridge.Models
{
    public class RfqQuoteInquiry
    {
        public string RequestId { get; private set; }
        public string RequestParty { get; private set; }
        public string ProductName { get; private set; }
        public ProductDetails ProductDetails { get; private set; }
        public double Quantity { get; private set; }
        public DateTime RequestExpirationDate { get; private set; }
        public string[] CounterParties { get; private set; }

        public RfqQuoteInquiry(
            string requestId,
            string requestParty,
            string productName,
            ProductDetails productDetails,
            double quantity,
            DateTime requestExpirationDate,
            params string[] counterParties)
        {
            RequestId = requestId;
            CounterParties = counterParties;
            RequestExpirationDate = requestExpirationDate;
            Quantity = quantity;
            ProductDetails = productDetails;
            ProductName = productName;
            RequestParty = requestParty;
        }

        public override string ToString()
        {
            return string.Format("[RfqQuoteInquiry: RequestId={0}, RequestParty={1}, ProductName={2}, ProductDetails={3}, Quantity={4}, RequestExpirationDate={5}, CounterParties={6}]", RequestId, RequestParty, ProductName, ProductDetails, Quantity, RequestExpirationDate, string.Join(", ", CounterParties));
        }
    }
}
