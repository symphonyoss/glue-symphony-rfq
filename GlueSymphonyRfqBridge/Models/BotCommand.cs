// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Collections.Generic;

namespace GlueSymphonyRfqBridge.Models
{
    public class BotCommand
    {
        public Dictionary<string, string> Arguments { get; private set; }

        public BotCommand(RfqCommand command)
        {
            Command = command;
            Arguments = new Dictionary<string, string>();
        }

        public string this[string parameter]
        {
            get
            {
                var result = Get(parameter);
                if (string.IsNullOrWhiteSpace(result))
                {
                    throw new ArgumentException(
                        "Required parameter " + parameter + " not present in message");
                }
                return result;
            }
        }

        public string Get(string parameter)
        {
            string result;
            Arguments.TryGetValue(parameter, out result);
            return result;
        }

        public RfqCommand Command { get; private set; }

        public BotCommand Add(string parameter, string value)
        {
            Arguments[parameter] = value;
            return this;
        }

}
}
