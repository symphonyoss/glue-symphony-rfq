// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System.Text.RegularExpressions;

namespace GlueSymphonyRfqBridge.Models
{
    namespace Extensions
    {
        public static class BotCommandExtensions
        {
            public static BotCommand WithRegexGroup(
                this BotCommand command,
                GroupCollection group,
                params string[] parameterNames)
            {
                foreach (var parameter in parameterNames)
                {
                    command = command.Add(parameter, group[parameter].Value);
                }
                return command;
            }
        }
    }
}

