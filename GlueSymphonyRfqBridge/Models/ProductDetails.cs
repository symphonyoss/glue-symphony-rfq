// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;

namespace GlueSymphonyRfqBridge.Models
{
    // Not used at the moment
    public class ProductDetails
    {
        public string AssetClass { get; set; }
        public string UnderlyingName { get; set; }
        public DateTime? Expiry { get; set; }

        public override string ToString()
        {
            return string.Format("[ProductDetails: AssetClass={0}, UnderlyingName={1}, Expiry={2}]", AssetClass, UnderlyingName, Expiry);
        }
    }
}
