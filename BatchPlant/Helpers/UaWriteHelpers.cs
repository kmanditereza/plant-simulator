using BatchPlant;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BatchPlant.Helpers
{
    /// <summary>
    /// Thread-safe helper class for writing values to OPC UA nodes.
    /// </summary>
    public static class UaWriteHelpers
    {
        private static readonly object _lock = new();

        /// <summary>
        /// Update a variable and flag it for data-change notification.
        /// </summary>
        /// <param name="node">The OPC UA node to update</param>
        /// <param name="value">The new value to write</param>
        public static void Write(BaseDataVariableState node, object value)
        {
            var ctx = BatchPlantNodeManager.ServerContext;

            lock (_lock)
            {
                node.Value = value;
                node.Timestamp = DateTime.UtcNow;
                node.StatusCode = StatusCodes.Good;
                node.ClearChangeMasks(ctx, false);
            }
        }
    }
}




