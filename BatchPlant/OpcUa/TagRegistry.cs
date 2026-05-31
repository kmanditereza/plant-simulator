using Opc.Ua;
using System.Collections.Concurrent;

namespace BatchPlant.OpcUa
{
    /// <summary>
    /// Central registry for all OPC UA tags. Process areas register their tags here,
    /// and the AddressSpaceBuilder creates OPC UA nodes from the registry.
    /// At runtime, components write values through this registry.
    /// </summary>
    public class TagRegistry
    {
        private readonly ConcurrentDictionary<string, TagDefinition> _definitions = new();
        private readonly ConcurrentDictionary<string, BaseDataVariableState> _nodes = new();
        private readonly ConcurrentDictionary<string, object> _pendingWrites = new();
        private readonly object _flushLock = new();

        private ISystemContext? _systemContext;

        public int TagCount => _definitions.Count;

        /// <summary>
        /// Set the OPC UA system context (called after address space is built).
        /// </summary>
        public void SetSystemContext(ISystemContext context)
        {
            _systemContext = context;
        }

        /// <summary>
        /// Define a new tag. Called during initialization by process area constructors.
        /// </summary>
        public TagDefinition Define(
            string name,
            BuiltInType dataType,
            string folderPath,
            string? unit = null,
            string? description = null,
            object? initialValue = null,
            bool writable = false)
        {
            var def = new TagDefinition(name, dataType, folderPath, unit, description, initialValue, writable);
            if (!_definitions.TryAdd(name, def))
            {
                throw new InvalidOperationException($"Tag '{name}' already defined");
            }
            return def;
        }

        /// <summary>
        /// Get all tag definitions (used by AddressSpaceBuilder).
        /// </summary>
        public IEnumerable<TagDefinition> GetAllDefinitions() => _definitions.Values;

        /// <summary>
        /// Associate an OPC UA node with a tag name (called by AddressSpaceBuilder).
        /// </summary>
        public void RegisterNode(string name, BaseDataVariableState node)
        {
            _nodes[name] = node;
        }

        /// <summary>
        /// Get the OPC UA node for a tag (for alarm system, etc.).
        /// </summary>
        public BaseDataVariableState? GetNode(string name)
        {
            _nodes.TryGetValue(name, out var node);
            return node;
        }

        /// <summary>
        /// Queue a value write. Writes are batched and flushed to OPC UA once per tick.
        /// This is the primary method equipment/process components use to update tag values.
        /// </summary>
        public void Write(string tagName, object value)
        {
            _pendingWrites[tagName] = value;
        }

        /// <summary>
        /// Read the current value of a tag.
        /// Returns the pending write value if one exists, otherwise the OPC UA node value.
        /// </summary>
        public object? Read(string tagName)
        {
            if (_pendingWrites.TryGetValue(tagName, out var pending))
                return pending;

            if (_nodes.TryGetValue(tagName, out var node))
                return node.Value;

            return null;
        }

        /// <summary>
        /// Read a tag value as a specific type.
        /// </summary>
        public T Read<T>(string tagName, T defaultValue = default!)
        {
            var val = Read(tagName);
            if (val is T typed) return typed;
            if (val != null)
            {
                try { return (T)Convert.ChangeType(val, typeof(T)); }
                catch { }
            }
            return defaultValue;
        }

        /// <summary>
        /// Flush all pending writes to OPC UA nodes. Called once per engine tick.
        /// Single lock acquisition for all writes reduces contention.
        /// </summary>
        public void Flush()
        {
            if (_pendingWrites.IsEmpty || _systemContext == null) return;

            lock (_flushLock)
            {
                foreach (var kvp in _pendingWrites)
                {
                    if (_nodes.TryGetValue(kvp.Key, out var node))
                    {
                        node.Value = kvp.Value;
                        node.Timestamp = DateTime.UtcNow;
                        node.StatusCode = Opc.Ua.StatusCodes.Good;
                        node.ClearChangeMasks(_systemContext, false);
                    }
                }
                _pendingWrites.Clear();
            }
        }
    }
}
