using Opc.Ua;
using Opc.Ua.Server;

namespace BatchPlant.OpcUa
{
    /// <summary>
    /// Thin OPC UA node manager that delegates address space construction to AddressSpaceBuilder.
    /// Replaces the monolithic BatchPlantNodeManager (~2,267 lines).
    /// </summary>
    public class PlantNodeManager : CustomNodeManager2
    {
        private readonly TagRegistry _registry;

        public PlantNodeManager(IServerInternal server, ApplicationConfiguration configuration, TagRegistry registry)
            : base(server, configuration, Namespaces.BatchPlant)
        {
            _registry = registry;
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
                {
                    references = new List<IReference>();
                    externalReferences[ObjectIds.ObjectsFolder] = references;
                }

                var builder = new AddressSpaceBuilder(_registry, Server, NamespaceIndexes[0]);
                var rootNodes = builder.Build(SystemContext, references);

                foreach (var node in rootNodes)
                {
                    AddPredefinedNode(SystemContext, node);
                }

                // Set the system context for tag flushing
                _registry.SetSystemContext(SystemContext);
            }
        }
    }
}
