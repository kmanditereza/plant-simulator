using Opc.Ua;
using Opc.Ua.Server;

namespace BatchPlant.OpcUa
{
    /// <summary>
    /// Builds the OPC UA address space from TagRegistry definitions.
    /// Creates folder hierarchy and BaseDataVariableState nodes automatically.
    /// Replaces the manual ~2000-line node construction in the old BatchPlantNodeManager.
    /// </summary>
    public class AddressSpaceBuilder
    {
        private readonly TagRegistry _registry;
        private readonly IServerInternal _server;
        private readonly ushort _namespaceIndex;
        private readonly Dictionary<string, FolderState> _folders = new();

        public AddressSpaceBuilder(TagRegistry registry, IServerInternal server, ushort namespaceIndex)
        {
            _registry = registry;
            _server = server;
            _namespaceIndex = namespaceIndex;
        }

        /// <summary>
        /// Build the entire address space from registry definitions.
        /// Returns the root-level folders to add to the node manager.
        /// </summary>
        public List<NodeState> Build(ISystemContext context, IList<IReference> externalReferences)
        {
            var rootNodes = new List<NodeState>();

            foreach (var tag in _registry.GetAllDefinitions())
            {
                // Ensure folder hierarchy exists
                var parentFolder = GetOrCreateFolderPath(context, tag.FolderPath, externalReferences, rootNodes);

                // Create the variable node
                var variable = CreateVariable(context, parentFolder, tag);

                // Register the node in the registry for runtime writes
                _registry.RegisterNode(tag.Name, variable);
            }

            Console.WriteLine($"[OPCUA] Built address space: {_registry.TagCount} tags in {_folders.Count} folders");
            return rootNodes;
        }

        private FolderState GetOrCreateFolderPath(
            ISystemContext context,
            string path,
            IList<IReference> externalReferences,
            List<NodeState> rootNodes)
        {
            if (_folders.TryGetValue(path, out var existing))
                return existing;

            var parts = path.Split('/');
            string currentPath = "";
            FolderState? parent = null;

            for (int i = 0; i < parts.Length; i++)
            {
                currentPath = i == 0 ? parts[i] : currentPath + "/" + parts[i];

                if (_folders.TryGetValue(currentPath, out var folder))
                {
                    parent = folder;
                    continue;
                }

                folder = new FolderState(parent)
                {
                    SymbolicName = parts[i],
                    ReferenceTypeId = ReferenceTypeIds.Organizes,
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                    NodeId = new NodeId(currentPath, _namespaceIndex),
                    BrowseName = new QualifiedName(parts[i], _namespaceIndex),
                    DisplayName = new LocalizedText("en", parts[i]),
                    WriteMask = AttributeWriteMask.None,
                    UserWriteMask = AttributeWriteMask.None,
                    EventNotifier = EventNotifiers.None
                };

                if (parent != null)
                {
                    parent.AddChild(folder);
                }
                else
                {
                    // Root-level folder: reference from Objects
                    folder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
                    externalReferences.Add(
                        new NodeStateReference(ReferenceTypeIds.Organizes, false, folder.NodeId));
                    rootNodes.Add(folder);
                }

                _folders[currentPath] = folder;
                parent = folder;
            }

            return parent!;
        }

        private BaseDataVariableState CreateVariable(ISystemContext context, FolderState parent, TagDefinition tag)
        {
            var nodeId = new NodeId($"{tag.FolderPath}/{tag.Name}", _namespaceIndex);
            var dataTypeId = GetDataTypeId(tag.DataType);

            var variable = new BaseDataVariableState(parent)
            {
                SymbolicName = tag.Name,
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = nodeId,
                BrowseName = new QualifiedName(tag.Name, _namespaceIndex),
                DisplayName = new LocalizedText("en", tag.Name),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                DataType = dataTypeId,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = tag.Writable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                UserAccessLevel = tag.Writable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                Historizing = false,
                Value = tag.InitialValue ?? GetDefaultValue(tag.DataType),
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };

            if (tag.Description != null)
            {
                variable.Description = new LocalizedText("en", tag.Description);
            }

            parent.AddChild(variable);
            return variable;
        }

        private static NodeId GetDataTypeId(BuiltInType type) => type switch
        {
            BuiltInType.Boolean => DataTypeIds.Boolean,
            BuiltInType.Int16 => DataTypeIds.Int16,
            BuiltInType.Int32 => DataTypeIds.Int32,
            BuiltInType.UInt16 => DataTypeIds.UInt16,
            BuiltInType.UInt32 => DataTypeIds.UInt32,
            BuiltInType.Float => DataTypeIds.Float,
            BuiltInType.Double => DataTypeIds.Double,
            BuiltInType.String => DataTypeIds.String,
            BuiltInType.DateTime => DataTypeIds.DateTime,
            _ => DataTypeIds.Double
        };

        private static object GetDefaultValue(BuiltInType type) => type switch
        {
            BuiltInType.Boolean => false,
            BuiltInType.Int16 => (short)0,
            BuiltInType.Int32 => 0,
            BuiltInType.UInt16 => (ushort)0,
            BuiltInType.UInt32 => (uint)0,
            BuiltInType.Float => 0.0f,
            BuiltInType.Double => 0.0,
            BuiltInType.String => "",
            BuiltInType.DateTime => DateTime.MinValue,
            _ => 0.0
        };
    }
}
