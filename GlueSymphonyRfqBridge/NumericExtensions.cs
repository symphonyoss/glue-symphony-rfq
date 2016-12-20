// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System.Globalization;

namespace GlueSymphonyRfqBridge
{
    namespace Extensions
    {
        public static class NumericExtensions
        {
            public static double ParseDouble(this string text)
            {
                return double.Parse(
                    text,
                    NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture);
            }
        }
    }
        
}
