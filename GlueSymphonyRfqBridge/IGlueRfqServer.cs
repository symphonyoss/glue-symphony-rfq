// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

namespace GlueSymphonyRfqBridge
{
    public interface IGlueRfqServer
    {
        void Start(IGlueRfqBridge bridge);
        void Stop();
    }
}
