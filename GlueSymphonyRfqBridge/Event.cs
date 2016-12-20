// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
namespace GlueSymphonyRfqBridge
{
    public class Event<T> : EventArgs where T : class
    {
        public Event(T data)
        {
            Data = data;
        }

        public T Data { get; private set; }
    }
}
