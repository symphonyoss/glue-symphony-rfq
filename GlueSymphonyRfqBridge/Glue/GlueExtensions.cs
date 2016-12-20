// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Collections.Generic;
using DOT.AGM;

namespace GlueSymphonyRfqBridge.Glue
{
    namespace Extensions
    {
        public static class GlueContextValueExtensions
        {
            public static string TryGetString(this Dictionary<string, IContextValue> args, string key)
            {
                IContextValue cv;
                if (!args.TryGetValue(key, out cv))
                {
                    return null;
                }
                return cv.Value.AsString;
            }

            public static double SafeGetDouble(this Dictionary<string, IContextValue> args, string key)
            {
                var value = args[key].Value;
                switch (value.Type)
                {
                    case AgmValueType.Int:
                        return value.AsInt;
                    case AgmValueType.Long:
                        return value.AsLong;
                    case AgmValueType.Double:
                        return value.AsDouble;
                    default:
                        throw new InvalidOperationException("Cannot get " + key + " as double from " + value);
                }
            }
        }
    }
}
