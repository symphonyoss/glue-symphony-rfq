// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System.Reflection;
using DOT.AGM;
using DOT.AGM.Client;
using DOT.AGM.Core;
using DOT.AGM.Core.Client;
using DOT.AGM.Core.Server;
using DOT.AGM.DOTTransport;
using DOT.AGM.Server;
using DOT.Core;
using DOT.Core.Configuration;
using DOT.Core.EventDispatcher;
using DOT.Core.Isolation.System.IO;
using DOT.Core.Serialization.Properties;
using DOT.Core.Util;
using DOT.Logging;

namespace GlueSymphonyRfqBridge.Glue
{
    /// <summary>
    /// Configures and starts/stops AGM Server and Client. Reads configuration from 'Config\agm.properties'
    /// </summary>
    class Glue
    {
        private static readonly ISmartLogger Logger = new SmartLogger(typeof(Glue));

        public IServer Server { get; private set; }
        public IClient Client { get; private set; }

        public void Start()
        {
            Logger.Info("Starting GLUE...");

            ApplicationGuiMethods.Touch();
            var propertiesContext = new PropertiesSerializationContext(CumulativeExpandVariables.Instance,
                                                                       new DefaultTypeResolver(GlobalTypeResolver.Instance),
                                                                       GlobalConfigurationFactoryLocator.Instance);

            var configPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var agmPropertiesFile = Path.Combine(configPath, "Config", "agm.properties");
            var agmProperties = GetProperties(agmPropertiesFile, propertiesContext);

            var transportFactory = new DOTTransportFactory();
            var agmConfiguration = new AgmConfiguration(transportFactory);
            var agmFactory = new ApplicationGuiMethods();
            agmFactory.Initialize(agmConfiguration);

            Client = SetupAgmClient(agmProperties, agmFactory, propertiesContext);
            Server = SetupAgmServer(agmProperties, agmFactory, propertiesContext);

            Logger.Info("GLUE started.");
        }

        public void Stop()
        {
            Logger.Info("Stopping GLUE...");

            if (Server != null)
            {
                Server.Stop();
                Server.Dispose();
            }

            if (Client != null)
            {
                Client.Stop();
                Client.Dispose();
            }

            Logger.Info("GLUE stopped.");
        }

        private static IServer SetupAgmServer(Properties agmProperties, IApplicationGuiMethods agmFactory, PropertiesSerializationContext propertiesContext)
        {
            var serverConfiguration = (ServerConfiguration)ConfigurationHelper.LoadServerConfiguration(agmProperties, propertiesContext);
            serverConfiguration.EventDispatcherFactory = new DefaultStandardEventDispatcherFactory();
            var server = agmFactory.CreateServer(serverConfiguration);
            server.Start();
            return server;
        }

        private static IClient SetupAgmClient(Properties agmProperties, IApplicationGuiMethods agmFactory, PropertiesSerializationContext propertiesContext)
        {
            var clientConfiguration = (ClientConfiguration)ConfigurationHelper.LoadClientConfiguration(agmProperties, propertiesContext);
            clientConfiguration.EventDispatcherFactory = new DefaultStandardEventDispatcherFactory();
            var client = agmFactory.CreateClient(clientConfiguration);
            client.Start();
            return client;
        }

        private static Properties GetProperties(string filePath, PropertiesSerializationContext propertiesContext)
        {
            var properties = PropertyParser.Parse(filePath);
            properties.ExpandEnvironmentVariables(propertiesContext.ExpandVariables);
            return properties;
        }
    }
}
