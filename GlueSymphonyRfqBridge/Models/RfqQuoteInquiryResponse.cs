// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

namespace GlueSymphonyRfqBridge.Models
{
    public class RfqQuoteInquiryResponse
    {
        public RfqResponseType ResponseType { get; private set; }
        public string ResponseMessage { get; private set; }
        public string RequestId { get; private set; }
        public string RequestParty { get; private set; }
        public string CounterParty { get; private set; }
        public string ProductName { get; private set; }
        public ProductDetails ProductDetails { get; private set; }
        public double? Quantity { get; private set; }
        public double? Price { get; private set; }

        public RfqQuoteInquiryResponse(
            RfqResponseType responseType,
            string responseMessage,
            string requestId,
            string requestParty,
            string counterParty,
            string productName,
            ProductDetails productDetails,
            double? quantity,
            double? price)
        {
            RequestParty = requestParty;
            ResponseMessage = responseMessage;
            Price = price;
            Quantity = quantity;
            ProductDetails = productDetails;
            ProductName = productName;
            CounterParty = counterParty;
            RequestId = requestId;
            ResponseType = responseType;
        }

        public override string ToString()
        {
            return string.Format("[RfqQuoteInquiryResponse: ResponseType={0}, ResponseMessage={1}, RequestId={2}, CounterParty={3}, ProductName={4}, ProductDetails={5}, Quantity={6}, Price={7}]", ResponseType, ResponseMessage, RequestId, CounterParty, ProductName, ProductDetails, Quantity, Price);
        }
    }
}
