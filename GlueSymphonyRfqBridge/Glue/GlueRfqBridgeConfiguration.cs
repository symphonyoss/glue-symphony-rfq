// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using DOT.AGM.Client;

namespace GlueSymphonyRfqBridge.Glue
{
    public class GlueRfqBridgeConfiguration
    {
        public GlueRfqBridgeConfiguration(
            IClient client)
        {
            Client = client;
        }

        public IClient Client { get; private set; }
    }
}