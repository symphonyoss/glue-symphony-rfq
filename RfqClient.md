#### Triggering an RFQ

RFQ (request for quote) is triggered when the user presses the *"Send RFQ"* button.

The client app should trigger an RFQ by subscribing on `T42.RFQ.QuoteInquiryStream`, passing:

|Field|Description|
|-----|-----------|
|`string requestParty`|The e-mail of the requesting party|
|`string[] counterParties`|The e-mails of the counter parties which the requesting party expects to receive quotes from|
|`string productName`|The name of the product for which this RFQ is made, usually an OTC, non-tradeable instrument|
|`double quantity`|The number of *units* of the product the requesting party wants to buy or sell. If `quantity` is `> 0` that's a *BUY*, otherwise a *SELL* RFQ request|
|`DateTime requestExpirationDate`|The absolute time when this RFQ expires|

#### Receiving Quotes

At some point, the app will start receiving quotes from one or more counter parties on the *same* `T42.RFQ.QuoteInquiryStream` stream. The stream data has the following shape:

|Field|Description|
|-----|-----------|
|`string responseType`|One of: `SetRequestId` (`requestId` only), `Quote` (almost all fields), `Expired` (`responseMessage` only), `Error` (`responseMessage` only)|
|`string requestId`|The RFQ request's generated ID|
|`string? responseMessage`|Usually the error message if `Error`|
|`string? counterParty`|The party sending the RFQ response. Initially, the RFQ bridge will stream `SetRequestId` with `requestId` set to an auto-generated value from the bridge.
|`string? productName`|The product in the RFQ (this will never be filled in in phase #1)|
|`double? quantity`|The quantity that's requested. A quote response can specify a different (lesser or bigger) value. If no value is passed, it's the same quantity as in the RFQ|
|`double? price`|The price quoted from the counter party|

#### Receiving from, and sending comments to counter party

The client app can subscribe for counter party comments on `T42.RFQ.CounterPartyCommentStream` passing its request party name.

Once subscribed, the request party can send a comment by calling `T42.RFQ.SendCommentToCounterParty` passing:

|Field|Description|
|-----|-----------|
|`requestParty`|Email of request party|
|`counterParty`|Target counter party's email|
|*`requestId`*|Optional, the request Id of an RFQ for which this comment is about, otherwise the comment is not tied to an RFQ|
|`comment`|Free-text message|

The counter party can reply at any time, again either to an RFQ-related or general comment. The reply will be published on `T42.RFQ.CounterPartyCommentStream`, and the data will have the same shape as the request fields in `T42.RFQ.SendCommentToCounterParty` (so optional `requestId`, and then `requestParty`, `counterParty`, `comment`).

### Appendix - GLUE Method Signatures

*Note:* All methods start with `T42.RFQ.`

|Method|Steam?|Accepts|Returns|
|------|------|-------|-------|
|`CounterPartyCommentStream`|Y|`string requestParty`|`string requestParty, string? requestId, string counterParty, comment`|
|`QuoteInquiryStream`|Y|`string requestParty, string[] counterParties, string productName, double quantity, DateTime requestExpirationDate`|`string requestId, string responseType, string? counterParty, string? productName, double? quantity, double? price`|
|`SendCommentToCounterParty`|N|`string requestParty, string? requestId, string counterParty, string comment`||


