using Opc.Ua;
using Opc.Ua.Server;

namespace BatchPlant.OpcUa
{
    /// <summary>
    /// OPC UA StandardServer subclass for the ketchup plant.
    /// Injects the TagRegistry into the PlantNodeManager.
    /// </summary>
    public class PlantServer : StandardServer
    {
        private readonly TagRegistry _registry;

        public PlantServer(TagRegistry registry)
        {
            _registry = registry;
        }

        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            var nodeManagers = new List<INodeManager>
            {
                new PlantNodeManager(server, configuration, _registry)
            };

            return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
        }

        protected override ServerProperties LoadServerProperties()
        {
            return new ServerProperties
            {
                ManufacturerName = "KetchupCo Industrial",
                ProductName = "Ketchup Plant DCS OPC UA Gateway",
                ProductUri = "http://opcfoundation.org/BatchPlant/v2.0",
                SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
                BuildNumber = Utils.GetAssemblyBuildNumber(),
                BuildDate = Utils.GetAssemblyTimestamp()
            };
        }
    }
}
