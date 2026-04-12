using Opc.Ua;

namespace BatchPlant.OpcUa
{
    /// <summary>
    /// Metadata for a single OPC UA tag. Tags are defined declaratively by process areas
    /// and built into the address space automatically by AddressSpaceBuilder.
    /// </summary>
    public class TagDefinition
    {
        /// <summary>Tag name as it appears in the address space (e.g., "PASTE_TK1_LVL", "TIT-301").</summary>
        public string Name { get; }

        /// <summary>OPC UA data type (Double, Boolean, Int32, String, etc.).</summary>
        public BuiltInType DataType { get; }

        /// <summary>Area folder path (e.g., "Area100_RawMaterials/PasteTank1").</summary>
        public string FolderPath { get; }

        /// <summary>Engineering unit (e.g., "degC", "%", "L/min", "bar").</summary>
        public string? Unit { get; }

        /// <summary>Human-readable description.</summary>
        public string? Description { get; }

        /// <summary>Initial value when server starts.</summary>
        public object? InitialValue { get; }

        /// <summary>Whether this tag is writable by OPC UA clients (e.g., commands, setpoints).</summary>
        public bool Writable { get; }

        public TagDefinition(
            string name,
            BuiltInType dataType,
            string folderPath,
            string? unit = null,
            string? description = null,
            object? initialValue = null,
            bool writable = false)
        {
            Name = name;
            DataType = dataType;
            FolderPath = folderPath;
            Unit = unit;
            Description = description;
            InitialValue = initialValue;
            Writable = writable;
        }
    }
}
