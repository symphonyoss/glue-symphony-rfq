// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Threading;
using DOT.Logging;
using GlueSymphonyRfqBridge.Glue;
using GlueSymphonyRfqBridge.Symphony;

namespace GlueSymphonyRfqBridge
{
    class Program
    {
        private static readonly ISmartLogger Logger = new SmartLogger(typeof(Program));

        public static volatile bool ShuttingDown;

        static void Main(string[] args)
        {
            try
            {
                new Program().Run(args);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        private void Run(string[] args)
        {
            IGlueRfqServer server = null;
            IGlueRfqBridge bridge = null;

            Glue.Glue glue = null;
            try
            {
                if (args.Length != 0 && args.Length != 2)
                {
                    var platform = Environment.OSVersion.Platform;
                    if (platform == PlatformID.MacOSX || platform == PlatformID.Unix)
                    {
                        Logger.Error("Usage: mono GlueSymphonyRfqBridge.exe [<certificateFilePath> <password>]");
                    }
                    else
                    {
                        Logger.Error("Usage: GlueSymphonyRfqBridge [<certificateFilePath> <password>]");
                    }
                    //return;
                }

                glue = new Glue.Glue();
                glue.Start();

                if (args.Length == 2)
                {
                    // ./Config/nws.gluerfq-cert.p12 changeit
                    var certFilePath = args[0];
                    var certPassword = args[1];
                    bridge = new SymphonyRfqBridge(
                        new SymphonyRfqBridgeConfiguration(certFilePath, certPassword));
                }
                else
                {
                    bridge = new GlueRfqBridge();
                }

                server = new GlueRfqServer(glue.Server);

                Logger.InfoFormat("Starting {0} bridge...", bridge.Name);
                bridge.Start();

                server.Start(bridge);

                var stopEvent = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    if (e.SpecialKey != ConsoleSpecialKey.ControlC)
                    {
                        return;
                    }
                    ShuttingDown = true;
                    Logger.Info("Ctrl-C pressed, terminating...");
                    stopEvent.Set();
                };
                Logger.Info("Press Ctrl-C to quit...");
                stopEvent.Wait();
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            finally
            {
                if (bridge != null)
                {
                    Logger.InfoFormat("Stopping {0} bridge...", bridge.Name);
                    bridge.Stop();
                    Logger.InfoFormat("Bridge {0} stopped.", bridge.Name);
                }

                if (server != null)
                {
                    server.Stop();
                }

                if (glue != null)
                {
                    glue.Stop();
                }
            }
        }
    }
}
