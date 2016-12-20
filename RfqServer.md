### UI

#####RFQ Auto-quote (title)

**Party**: [ single-select combo w/ les, stoyan ]
**Product**: [ editable text box ]
[ **Subscribe/Unsubscribe** ] // button which toggles - un/subscribes for prices)

**Best Bid and Offer Prices**
("rolling" window, showing last 3 prices)

| Time | Bid | Ask |
|------|-----|-----|
| T    | X   | Y   | (this one highlighted)
| T-1  | X'  | Y'  |
| T-2  | X"  | Y"  |

**Spread**: [ edit box ] // this gets added/subtracted from the top price

**[ ] Auto-quote** (checkbox)

**Message history**

| Time | Request Party | Product | Quantity |

When party is selected and product is typed, user can click [ Subscribe for prices ], which will actually subscribe for prices on the `T42.MarketStream.Subscribe` stream, as it does on the Portfolio demo.

While there are not prices, the Auto-quote checkbox will be disabled. As soon as prices start streaming, it will become enabled and when checked on,  the app will subscribe for quote requests.

When an RFQ is received, if it's not for the typed in product, the app should auto-reply "Not quoting product 'ProductName' at the moment". If it's the same product, the app should send a quote response by adjusting the price with the specified spread value:
	- if request party is buying, add spread to ask price
	- if request party is selling, subtract spread from bid price

#### Subscribing for RFQs

The app subscribes for RFQ requests by subscribing on `T42.RFQ.QuoteRequestStream` passing `counterParty`, which should contain the e-mail of the counter party which wants to listen for RFQ requests.

RFQs will be pushed to that stream and will contain the following information:

|Field|Description|
|-----|-----------|
|`string requestParty`|E-mail of the party sending the RFQ request|
|`string requestId`|ID of the request, needed to send replies back|
|`string productName`|Name of the product in RFQ, usually an OTC|
|`double quantity`|The number of *units* of the product the requesting party wants to buy or sell. If `quantity` is `> 0` that's a *BUY*, otherwise a *SELL* RFQ request|
|`DateTime requestExpirationDate`|The absolute time when this RFQ expires|

#### Sending Quotes

The app can send a quote by calling the `T42.RFQ.SendQuoteResponse`, passing the following parameters:

|Field|Description|
|-----|-----------|
|`string requestId`|The request ID the app got from the `T42.RFQ.QuoteRequestStream` subscription|
|`string requestParty`|The request party...|
|`string? responseMessage`|Optional, don't send it|
|`string counterParty`|The app's party which sends the response, should equal the `counterParty` from the request|
|`string? productName`|Optional, don't send it|
|`double? quantity`|Optional, don't send it|
|`double price`|The price the app quotes the user|

#### Receiving from, and sending comments to request party

The client app can subscribe for request party comments on `T42.RFQ.RequestPartyCommentStream` passing its counter party name.

The request party comments will be pushed to the stream and will have the following fields:

|Field|Description|
|-----|-----------|
|`requestParty`|Email of request party|
|`counterParty`|Target counter party's email|
|*`requestId`*|Optional, the request Id of an RFQ for which this comment is about, otherwise the comment is not tied to an RFQ|
|`comment`|Free-text message|

The counter party can reply at any time, again either to an RFQ-related or general comment. To reply, invoke `T42.RFQ.SendCommentToRequestParty` passing `requestParty`, `counterParty`, `comment`, and optionally the `requestId`.

### Appendix - GLUE Method Signatures

*Note:* All methods start with `T42.RFQ.`

|Method|Steam?|Accepts|Returns|
|------|------|-------|-------|
|`RequestPartyCommentStream`|Y|`string requestParty`|`string requestParty, string? requestId, string counterParty, comment`|
|`QuoteRequestStream`|Y|`string counterParty`|`string requestId, string requestId, string counterParty, string productName, double quantity, DateTime requestExpirationDate`|
|`SendCommentToRequestParty`|N|`string requestParty, string? requestId, string counterParty, string comment`||


