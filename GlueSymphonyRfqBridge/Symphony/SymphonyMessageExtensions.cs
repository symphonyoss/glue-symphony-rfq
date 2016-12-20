// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Globalization;
using GlueSymphonyRfqBridge.Models;

namespace GlueSymphonyRfqBridge.Symphony
{
    namespace Extensions
    {
        public static class SymphonyMessageExtensions
        {
            // rtc stoyan@tick42.com to lspiro@tick42.com re @1 Are you kidding me?
            // rtc stoyan@tick42.com to lspiro@tick42.com Are you kidding me?
            public static string ToRequestToCounterPartyCommentMessage(this RfqComment comment)
            {
                if (string.IsNullOrEmpty(comment.RequestId))
                {
                    return string.Format("rtc {0} to {1} {2}", 
                        comment.RequestParty, 
                        comment.CounterParty, 
                        comment.Comment);
                }
                return string.Format("rtc {0} to {1} wrt @{2} {3}", 
                    comment.RequestParty, 
                    comment.CounterParty, 
                    comment.RequestId, 
                    comment.Comment);
            }

            // ctr lspiro@tick42.com to stoyan@tick42.com re @1 Nope
            // ctr lspiro@tick42.com to stoyan@tick42.com Nope
            public static string ToCounterToRequestPartyCommentMessage(this RfqComment comment)
            {
                if (string.IsNullOrEmpty(comment.RequestId))
                {
                    return string.Format("ctr {0} to {1} {2}", 
                        comment.CounterParty,
                        comment.RequestParty, 
                        comment.Comment);
                }
                return string.Format("ctr {0} to {1} wrt @{2} {3}", 
                    comment.CounterParty,
                    comment.RequestParty, 
                    comment.RequestId, 
                    comment.Comment);
            }

            // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR (15 min)
            // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR
            public static string ToMessage(
                this RfqQuoteInquiry rfq,
                string targetParty)
            {
                var result = string.Format("rfq {0}:{1} at {2} {3} {4} {5} {6}",
                                           rfq.RequestParty,
                                           rfq.RequestId,
                                           targetParty,
                                           rfq.Quantity > 0 ? "buy" : "sell",
                                           ToHumanReadableQuantity(Math.Abs(rfq.Quantity)),
                                           rfq.ProductName,
                                           ToHumanReadableExpiry(rfq.RequestExpirationDate));
                return result;
            }

            public static string ToMessage(this RfqQuoteInquiryResponse quote)
            {
                switch (quote.ResponseType)
                {
                    // this one should not happen
                    case RfqResponseType.SetRequestId:
                        return string.Format("id {0}:{1}", quote.RequestParty, quote.RequestId);
                    case RfqResponseType.Expired:
                        return string.Format("expired {0}:{1}", quote.RequestParty, quote.RequestId);
                    case RfqResponseType.Error:
                        return string.Format("error {0}:{1} {2}", quote.RequestParty, quote.RequestId, quote.ResponseMessage);
                    case RfqResponseType.Quote:
                        return ToQuoteMessage(quote);
                    default:
                        throw new NotImplementedException();
                }
            }

            // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.3 for 200m
            // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.5
            private static string ToQuoteMessage(RfqQuoteInquiryResponse quote)
            {
                var result = string.Format("quote from {0} to {1}:{2} @ {3}",
                                           quote.CounterParty,
                                           quote.RequestParty,
                                           quote.RequestId,
                                           string.Format(CultureInfo.InvariantCulture, "{0:N4}", quote.Price.Value));
                if (quote.Quantity.HasValue)
                {
                    result += " for " + ToHumanReadableQuantity(quote.Quantity.Value);
                }
                return result;
            }

            private static string ToHumanReadableQuantity(double quantity)
            {
                // TODO: deal with floating numbers; for now just assume longs
                var qty = Math.Abs((long)Math.Truncate(quantity));
                if (qty > 1000000 && (qty % 1000000) == 0)
                {
                    return (qty / 1000000) + "m";
                }
                if (qty > 1000 && (qty % 1000) == 0)
                {
                    return (qty / 1000) + "k";
                }
                return string.Format(CultureInfo.InvariantCulture, "{0:N0}", quantity);
            }

            private static string ToHumanReadableExpiry(DateTime dateTime)
            {
                var span = dateTime - DateTime.UtcNow;

                var mins = (long)span.TotalMinutes;
                if (mins >= 60 && (mins % 60) == 0)
                {
                    var hours = mins / 60;
                    return string.Format("({0} hour{1})",
                                         hours,
                                         hours == 1 ? string.Empty : "s");
                }

                var secs = (long)span.TotalSeconds;
                if (secs >= 60 && (secs % 60) == 0)
                {
                    mins = secs / 60;
                    return string.Format("({0} min)", mins);
                }
                return string.Format("({0} sec)", secs);
            }
        }
    }
}
