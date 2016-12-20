// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;

namespace GlueSymphonyRfqBridge.Symphony
{
    public class SymphonyRfqBridgeConfiguration
    {
        public SymphonyRfqBridgeConfiguration(
            string botCertificateFilePath,
            string botCertificatePassword)
        {
            BotCertificateFilePath = botCertificateFilePath;
            BotCertificatePassword = botCertificatePassword;
            BaseApiUrl = "https://foundation-dev-api.symphony.com";
            BasePodUrl = "https://foundation-dev.symphony.com";
            TimeoutInMillis = 35000; // because https://github.com/symphonyoss/RestApiClient/issues/22
            DefaultRfqExpiry = TimeSpan.FromMinutes(15);
        }

        public string BotCertificateFilePath { get; private set; }
        public string BotCertificatePassword { get; private set; }

        public string BaseApiUrl { get; set; }
        public string BasePodUrl { get; set; }

        public int TimeoutInMillis { get; set; }
        public TimeSpan DefaultRfqExpiry { get; set; }

        public override string ToString()
        {
            return string.Format("[SymphonyRfqBridgeConfiguration: BotCertificateFilePath={0}, BotCertificatePassword={1}, BaseApiUrl={2}, BasePodUrl={3}, TimeoutInMillis={4}]", BotCertificateFilePath, BotCertificatePassword, BaseApiUrl, BasePodUrl, TimeoutInMillis);
        }
    }
}
