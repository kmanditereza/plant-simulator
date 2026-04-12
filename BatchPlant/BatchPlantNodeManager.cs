using BatchPlant.Alarms;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BatchPlant.Alarms;

namespace BatchPlant
{
    class BatchPlantNodeManager : CustomNodeManager2
    {
        /// <summary>
        /// The one and only ServerSystemContext all writers should use.
        /// </summary>
        public static ServerSystemContext ServerContext { get; private set; }

        /// <summary>
        /// The alarm condition manager for OPC UA Part 9 alarms.
        /// </summary>
        public static AlarmConditionManager AlarmManager { get; private set; }


        public BatchPlantNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        :
       base(server, configuration)
        {
            ServerContext = SystemContext;   // ⭐ save for writers
            SystemContext.NodeIdFactory = this;

            // set one namespace for the type model and one names for dynamically created nodes.
            string[] namespaceUrls = new string[2];
            namespaceUrls[0] = BatchPlant.Namespaces.BatchPlant;
            namespaceUrls[1] = BatchPlant.Namespaces.BatchPlant + "/Instance";
            SetNamespaces(namespaceUrls);

            // get the configuration for the node manager.
            m_configuration = configuration.ParseExtension<BatchPlantServerConfiguration>();

            // use suitable defaults if no configuration exists.
            if (m_configuration == null)
            {
                m_configuration = new BatchPlantServerConfiguration();
            }
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {

            lock (Lock)
            {
                IList<IReference> objectsFolderRefs = null;

                if (!externalReferences.TryGetValue(Opc.Ua.ObjectIds.ObjectsFolder, out objectsFolderRefs))
                {
                    externalReferences[Opc.Ua.ObjectIds.ObjectsFolder] = objectsFolderRefs = new List<IReference>();
                }


                /******************** ADD RAW MATERIAL TANKS NODES TO THE ADDRESS SPACE  *******************/
                //TANK 1

                // 1) Create the new dynamic folder to appear under Objects 
                TK101 = new FolderState(null)
                {
                    SymbolicName = "TK101",
                    NodeId = new NodeId("TK101", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TK101", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Tank1"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                // 2) Attach Tank 1 under the standard ObjectsFolder
                TK101.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, TK101.NodeId));


                // 3) Create children under Tank 1 
                LT101 = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "LT101",
                    NodeId = new NodeId("LT101", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("LT101", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "LT101"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                FCV101 = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "FCV101",
                    NodeId = new NodeId("FCV101", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FCV101", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FCV101"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P101_START = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "P101_START",
                    NodeId = new NodeId("P101_START", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P101_START", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P101_START"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P101_RUN = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "P101_RUN",
                    NodeId = new NodeId("P101_RUN", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P101_RUN", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P101_RUN"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P101_FAULT = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "P101_FAULT",
                    NodeId = new NodeId("P101_FAULT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P101_FAULT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P101_FAULT"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TT101 = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "TT101",
                    NodeId = new NodeId("TT101", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT101", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT101"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                PT101 = new BaseDataVariableState(TK101)
                {
                    SymbolicName = "HT101",
                    NodeId = new NodeId("HT101", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HT101", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HT101"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };


                TK101.AddChild(LT101);
                TK101.AddChild(FCV101);
                TK101.AddChild(P101_START);
                TK101.AddChild(P101_RUN);
                TK101.AddChild(P101_FAULT);
                TK101.AddChild(TT101);
                TK101.AddChild(PT101);

                // Finally, register the dynamic folder with the server
                AddPredefinedNode(SystemContext, TK101);


                //TANK 2
                TK102 = new FolderState(null)
                {
                    SymbolicName = "TK102",
                    NodeId = new NodeId("TK102", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TK102", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Tank2"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                // 2) Attach Tank 2 under the standard ObjectsFolder
                TK102.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, TK102.NodeId));

                // 3) Create children under Tank 2
                LT102 = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "LT102",
                    NodeId = new NodeId("LT102", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("LT102", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "LT102"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                FCV102 = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "FCV102",
                    NodeId = new NodeId("FCV102", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FCV102", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FCV102"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P102_START = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "P102_START",
                    NodeId = new NodeId("P102_START", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P102_START", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P102_START"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P102_RUN = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "P102_RUN",
                    NodeId = new NodeId("P102_RUN", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P102_RUN", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P102_RUN"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P102_FAULT = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "P102_FAULT",
                    NodeId = new NodeId("P102_FAULT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P102_FAULT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P102_FAULT"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TT102 = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "TT102",
                    NodeId = new NodeId("TT102", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT102", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT102"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                PT102 = new BaseDataVariableState(TK102)
                {
                    SymbolicName = "HT102",
                    NodeId = new NodeId("HT102", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HT102", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HT102"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TK102.AddChild(LT102);
                TK102.AddChild(FCV102);
                TK102.AddChild(P102_START);
                TK102.AddChild(P102_RUN);
                TK102.AddChild(P102_FAULT);
                TK102.AddChild(TT102);
                TK102.AddChild(PT102);
                // Finally, register the dynamic folder with the server
                AddPredefinedNode(SystemContext, TK102);


                //TANK 3
                TK103 = new FolderState(null)
                {
                    SymbolicName = "TK103",
                    NodeId = new NodeId("TK103", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TK103", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Tank3"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                // 2) Attach Tank 3 under the standard ObjectsFolder
                TK103.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, TK103.NodeId));

                // 3) Create children under Tank 3
                LT103 = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "LT103",
                    NodeId = new NodeId("LT103", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("LT103", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "LT103"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                FCV103 = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "FCV103",
                    NodeId = new NodeId("FCV103", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FCV103", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FCV103"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P103_START = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "P103_START",
                    NodeId = new NodeId("P103_START", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P103_START", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P103_START"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P103_RUN = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "P103_RUN",
                    NodeId = new NodeId("P103_RUN", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P103_RUN", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P103_RUN"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P103_FAULT = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "P103_FAULT",
                    NodeId = new NodeId("P103_FAULT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P103_FAULT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P103_FAULT"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TT103 = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "TT103",
                    NodeId = new NodeId("TT103", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT103", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT103"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                PT103 = new BaseDataVariableState(TK103)
                {
                    SymbolicName = "HT103",
                    NodeId = new NodeId("HT103", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HT103", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HT103"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TK103.AddChild(LT103);
                TK103.AddChild(FCV103);
                TK103.AddChild(P103_START);
                TK103.AddChild(P103_RUN);
                TK103.AddChild(P103_FAULT);
                TK103.AddChild(TT103);
                TK103.AddChild(PT103);
                // Finally, register the dynamic folder with the server
                AddPredefinedNode(SystemContext, TK103);



                //******************** ADD BATCH CONTROL NODES TO THE ADDRESS SPACE ****************************/
                // 1) Create the new dynamic folder to appear under Objects
                batchControl = new FolderState(null)
                {
                    SymbolicName = "batchControl",
                    NodeId = new NodeId("batchControl", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("batchControl", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batch Control"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                // 2) Attach under the standard ObjectsFolder
                batchControl.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, batchControl.NodeId));

                // 3) Create children under Tank 3
                BATCH_START = new BaseDataVariableState(batchControl)
                {
                    SymbolicName = "BATCH_START",
                    NodeId = new NodeId("BATCH_START", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BATCH_START", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "BATCH_START"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                BATCH_PAUSE = new BaseDataVariableState(batchControl)
                {
                    SymbolicName = "BATCH_PAUSE",
                    NodeId = new NodeId("BATCH_PAUSE", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BATCH_PAUSE", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "BATCH_PAUSE"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                BATCH_ABORT = new BaseDataVariableState(batchControl)
                {
                    SymbolicName = "BATCH_ABORT",
                    NodeId = new NodeId("BATCH_ABORT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BATCH_ABORT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "BATCH_ABORT"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                batchControl.AddChild(BATCH_START);
                batchControl.AddChild(BATCH_PAUSE);
                batchControl.AddChild(BATCH_ABORT);
                // Finally, register the dynamic folder with the server
                AddPredefinedNode(SystemContext, batchControl);

                //******************** ADD BATCH DETAILS NODES TO THE ADDRESS SPACE ****************************/

                // 1) Create the new dynamic folder to appear under Objects
                batchProduction = new FolderState(null)
                {
                    SymbolicName = "batchProduction",
                    NodeId = new NodeId("batchProduction", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("batchProduction", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batch Production"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                // 2) Attach under the standard ObjectsFolder
                batchProduction.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, batchProduction.NodeId));

                // Create a new variable node
                batch_code = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "BatchCode",
                    NodeId = new NodeId("BatchCode", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BatchCode", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batch Code"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "B012345",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Create a new variable node
                product_code = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "ProductCode",
                    NodeId = new NodeId("ProductCode", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("ProductCode", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Product Code"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "PC012345",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };


                // Create a new variable node
                batch_status = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "BatchStatus",
                    NodeId = new NodeId("BatchStatus", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BatchStatus", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batch Status"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Running",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                batches_done = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "BatchesDone",
                    NodeId = new NodeId("BatchesDone", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BatchesDone", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batches Done"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 5,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                batches_required = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "BatchesRequired",
                    NodeId = new NodeId("BatchesRequired", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BatchesRequired", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batches Required"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 10,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                batch_start_time = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "BatchStartTime",
                    NodeId = new NodeId("BatchStartTime", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BatchStartTime", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batch Start Time"),
                    DataType = DataTypeIds.DateTime,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = DateTime.Now,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                batch_end_time = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "BatchEndTime",
                    NodeId = new NodeId("BatchEndTime", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("BatchEndTime", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Batch End Time"),
                    DataType = DataTypeIds.DateTime,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = DateTime.Now,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material1_target = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material1Target",
                    NodeId = new NodeId("Material1Target", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material1Target", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 1 Target"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 100.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material2_target = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material2Target",
                    NodeId = new NodeId("Material2Target", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material2Target", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 2 Target"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 200.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material3_target = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material3Target",
                    NodeId = new NodeId("Material3Target", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material3Target", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 3 Target"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 300.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material1_actual = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material1Actual",
                    NodeId = new NodeId("Material1Actual", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material1Actual", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 1 Actual"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 95.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material2_actual = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material2Actual",
                    NodeId = new NodeId("Material2Actual", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material2Actual", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 2 Actual"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 210.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material3_actual = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material3Actual",
                    NodeId = new NodeId("Material3Actual", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material3Actual", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 3 Actual"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 290.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material1_name = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material1Name",
                    NodeId = new NodeId("Material1Name", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material1Name", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 1 Name"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Material A",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material2_name = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material2Name",
                    NodeId = new NodeId("Material2Name", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material2Name", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 2 Name"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Material B",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                material3_name = new BaseDataVariableState(batchProduction)
                {
                    SymbolicName = "Material3Name",
                    NodeId = new NodeId("Material3Name", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Material3Name", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Material 3 Name"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Material C",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };



                batchProduction.AddChild(batch_code);
                batchProduction.AddChild(product_code);
                batchProduction.AddChild(batch_status);
                batchProduction.AddChild(batches_done);
                batchProduction.AddChild(batches_required);
                batchProduction.AddChild(batch_start_time);
                batchProduction.AddChild(batch_end_time);
                batchProduction.AddChild(material1_target);
                batchProduction.AddChild(material2_target);
                batchProduction.AddChild(material3_target);
                batchProduction.AddChild(material1_actual);
                batchProduction.AddChild(material2_actual);
                batchProduction.AddChild(material3_actual);
                batchProduction.AddChild(material1_name);
                batchProduction.AddChild(material2_name);
                batchProduction.AddChild(material3_name);

                // Finally, register the dynamic folder with the server
                AddPredefinedNode(SystemContext, batchProduction);


                //******************** ADD MIXER NODES TO THE ADDRESS SPACE ****************************/

                // 1) Create the new dynamic folder to appear under Objects

                MX101 = new FolderState(null)
                {
                    SymbolicName = "MX101",
                    NodeId = new NodeId("MX101", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MX101", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MX101"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                // 2) Attach under the standard ObjectsFolder
                MX101.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, MX101.NodeId));

                // 3) Create children under MX101
                TT201 = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "TT201",
                    NodeId = new NodeId("TT201", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT201", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT201"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                LT201 = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "LT201",
                    NodeId = new NodeId("LT201", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("LT201", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "LT201"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                VISC201 = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "VISC201",
                    NodeId = new NodeId("VISC201", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("VISC201", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "VISC201"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TURB201 = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "TURB201",
                    NodeId = new NodeId("TURB201", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TURB201", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TURB201"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_START = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_START",
                    NodeId = new NodeId("MT201_START", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_START", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_START"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_RUN = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_RUN",
                    NodeId = new NodeId("MT201_RUN", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_RUN", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_RUN"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_FAULT = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_FAULT",
                    NodeId = new NodeId("MT201_FAULT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_FAULT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_FAULT"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_RPM = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_RPM",
                    NodeId = new NodeId("MT201_RPM", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_RPM", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_RPM"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_TRQ = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_TRQ",
                    NodeId = new NodeId("MT201_TRQ", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_TRQ", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_TRQ"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_PWR = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_PWR",
                    NodeId = new NodeId("MT201_PWR", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_PWR", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_PWR"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_AMP = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_AMP",
                    NodeId = new NodeId("MT201_AMP", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_AMP", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_AMP"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MT201_VIB = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MT201_VIB",
                    NodeId = new NodeId("MT201_VIB", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MT201_VIB", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MT201_VIB"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MX101_STATE = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MX101_STATE",
                    NodeId = new NodeId("MX101_STATE", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MX101_STATE", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MX101_STATE"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Stopped",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MX101_MODE = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "MX101_MODE",
                    NodeId = new NodeId("MX101_MODE", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("MX101_MODE", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "MX101_MODE"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Manual",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                mixer_status = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "mixer_status",
                    NodeId = new NodeId("mixer_status", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("mixer_status", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "mixer_status"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Idle",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                mixing_time = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "mixing_time",
                    NodeId = new NodeId("mixing_time", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("mixing_time", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "mixing_time"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                mixer_energy_consumed = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "mixer_energy_consumed",
                    NodeId = new NodeId("mixer_energy_consumed", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("mixer_energy_consumed", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "mixer_energy_consumed"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                SP_MX_INITIAL_SPEED = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "SP_MX_INITIAL_SPEED",
                    NodeId = new NodeId("SP_MX_INITIAL_SPEED", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SP_MX_INITIAL_SPEED", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SP_MX_INITIAL_SPEED"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                SP_MX_FINAL_SPEED = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "SP_MX_FINAL_SPEED",
                    NodeId = new NodeId("SP_MX_FINAL_SPEED", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SP_MX_FINAL_SPEED", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SP_MX_FINAL_SPEED"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                SP_MX_AMBIENT_TEMP = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "SP_MX_AMBIENT_TEMP",
                    NodeId = new NodeId("SP_MX_AMBIENT_TEMP", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SP_MX_AMBIENT_TEMP", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SP_MX_AMBIENT_TEMP"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                SP_MX_INITIAL_VISC = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "SP_MX_INITIAL_VISC",
                    NodeId = new NodeId("SP_MX_INITIAL_VISC", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SP_MX_INITIAL_VISC", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SP_MX_INITIAL_VISC"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                SP_MX_TARGET_VISC = new BaseDataVariableState(MX101)
                {
                    SymbolicName = "SP_MX_TARGET_VISC",
                    NodeId = new NodeId("SP_MX_TARGET_VISC", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SP_MX_TARGET_VISC", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SP_MX_TARGET_VISC"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                MX101.AddChild(TT201);
                MX101.AddChild(LT201);
                MX101.AddChild(VISC201);
                MX101.AddChild(TURB201);
                MX101.AddChild(MT201_START);
                MX101.AddChild(MT201_RUN);
                MX101.AddChild(MT201_FAULT);
                MX101.AddChild(MT201_RPM);
                MX101.AddChild(MT201_TRQ);
                MX101.AddChild(MT201_PWR);
                MX101.AddChild(MT201_AMP);
                MX101.AddChild(MT201_VIB);
                MX101.AddChild(MX101_STATE);
                MX101.AddChild(MX101_MODE);
                MX101.AddChild(mixer_status);
                MX101.AddChild(mixing_time);
                MX101.AddChild(mixer_energy_consumed);
                MX101.AddChild(SP_MX_INITIAL_SPEED);
                MX101.AddChild(SP_MX_FINAL_SPEED);
                MX101.AddChild(SP_MX_AMBIENT_TEMP);
                MX101.AddChild(SP_MX_INITIAL_VISC);
                MX101.AddChild(SP_MX_TARGET_VISC);

                // ---- ReportParameters subfolder ----
                MX101_ReportParameters = new FolderState(MX101)
                {
                    SymbolicName = "ReportParameters",
                    NodeId = new NodeId("MX101_ReportParameters", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("ReportParameters", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "ReportParameters"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                MX_RPT_AVG_AGITATOR_SPEED = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Average_Agitator_Speed",
                    NodeId = new NodeId("MX_RPT_AVG_AGITATOR_SPEED", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Average_Agitator_Speed", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Average_Agitator_Speed"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_AVG_MOTOR_POWER = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Average_Motor_Power",
                    NodeId = new NodeId("MX_RPT_AVG_MOTOR_POWER", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Average_Motor_Power", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Average_Motor_Power"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_FINAL_HOMOGENEITY = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Final_Homogeneity",
                    NodeId = new NodeId("MX_RPT_FINAL_HOMOGENEITY", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Final_Homogeneity", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Final_Homogeneity"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_FINAL_TEMPERATURE = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Final_Temperature",
                    NodeId = new NodeId("MX_RPT_FINAL_TEMPERATURE", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Final_Temperature", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Final_Temperature"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_FINAL_TURBIDITY = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Final_Turbidity",
                    NodeId = new NodeId("MX_RPT_FINAL_TURBIDITY", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Final_Turbidity", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Final_Turbidity"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_FINAL_VISCOSITY = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Final_Viscosity",
                    NodeId = new NodeId("MX_RPT_FINAL_VISCOSITY", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Final_Viscosity", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Final_Viscosity"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_INITIAL_VISCOSITY = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Initial_Viscosity",
                    NodeId = new NodeId("MX_RPT_INITIAL_VISCOSITY", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Initial_Viscosity", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Initial_Viscosity"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_MAX_MOTOR_POWER = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Max_Motor_Power",
                    NodeId = new NodeId("MX_RPT_MAX_MOTOR_POWER", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Max_Motor_Power", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Max_Motor_Power"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX_RPT_MIX_DURATION = new BaseDataVariableState(MX101_ReportParameters)
                {
                    SymbolicName = "Mix_Duration",
                    NodeId = new NodeId("MX_RPT_MIX_DURATION", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("Mix_Duration", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "Mix_Duration"),
                    DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };

                MX101_ReportParameters.AddChild(MX_RPT_AVG_AGITATOR_SPEED);
                MX101_ReportParameters.AddChild(MX_RPT_AVG_MOTOR_POWER);
                MX101_ReportParameters.AddChild(MX_RPT_FINAL_HOMOGENEITY);
                MX101_ReportParameters.AddChild(MX_RPT_FINAL_TEMPERATURE);
                MX101_ReportParameters.AddChild(MX_RPT_FINAL_TURBIDITY);
                MX101_ReportParameters.AddChild(MX_RPT_FINAL_VISCOSITY);
                MX101_ReportParameters.AddChild(MX_RPT_INITIAL_VISCOSITY);
                MX101_ReportParameters.AddChild(MX_RPT_MAX_MOTOR_POWER);
                MX101_ReportParameters.AddChild(MX_RPT_MIX_DURATION);
                MX101.AddChild(MX101_ReportParameters);

                // Finally, register the dynamic folder with the server
                AddPredefinedNode(SystemContext, MX101);

                // ************************ HEAT EXCHANGER NODES ************************
                HX301 = new FolderState(null)
                {
                    SymbolicName = "HX301",
                    NodeId = new NodeId("HX301", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HX301", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HX301"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                HX301.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, HX301.NodeId));

                // Temperatures and pressures
                TT301 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "TT301",
                    NodeId = new NodeId("TT301", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT301", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT301"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 22.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TT302 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "TT302",
                    NodeId = new NodeId("TT302", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT302", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT302"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 22.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                PT301 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "PT301",
                    NodeId = new NodeId("PT301", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("PT301", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "PT301"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.3,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Flows
                FT301 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "FT301",
                    NodeId = new NodeId("FT301", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FT301", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FT301"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                FT302 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "FT302",
                    NodeId = new NodeId("FT302", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FT302", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FT302"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                FT303 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "FT303",
                    NodeId = new NodeId("FT303", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FT303", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FT303"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Steam valve
                SV301 = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "SV301",
                    NodeId = new NodeId("SV301", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SV301", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SV301"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Pump signals
                P301_START = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "P301_START",
                    NodeId = new NodeId("P301_START", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P301_START", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P301_START"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P301_RUN = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "P301_RUN",
                    NodeId = new NodeId("P301_RUN", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P301_RUN", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P301_RUN"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P301_FAULT = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "P301_FAULT",
                    NodeId = new NodeId("P301_FAULT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P301_FAULT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P301_FAULT"),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                P301_SPEED = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "P301_SPEED",
                    NodeId = new NodeId("P301_SPEED", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("P301_SPEED", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "P301_SPEED"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // HX state/mode/status/energy
                HX301_STATE = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "HX301_STATE",
                    NodeId = new NodeId("HX301_STATE", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HX301_STATE", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HX301_STATE"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 1,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                HX301_MODE = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "HX301_MODE",
                    NodeId = new NodeId("HX301_MODE", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HX301_MODE", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HX301_MODE"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 1,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                hx_status = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "hx_status",
                    NodeId = new NodeId("hx_status", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("hx_status", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "hx_status"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Idle",
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                heating_time = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "heating_time",
                    NodeId = new NodeId("heating_time", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("heating_time", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "heating_time"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 1200,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                hx_energy_consumed = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "hx_energy_consumed",
                    NodeId = new NodeId("hx_energy_consumed", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("hx_energy_consumed", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "hx_energy_consumed"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // PID and setpoint
                SP_HX_TEMP = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "SP_HX_TEMP",
                    NodeId = new NodeId("SP_HX_TEMP", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("SP_HX_TEMP", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "SP_HX_TEMP"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 90.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                PID_HX_OUTPUT = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "PID_HX_OUTPUT",
                    NodeId = new NodeId("PID_HX_OUTPUT", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("PID_HX_OUTPUT", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "PID_HX_OUTPUT"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Fouling monitoring
                HX_FOULING_INDEX = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "HX_FOULING_INDEX",
                    NodeId = new NodeId("HX_FOULING_INDEX", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HX_FOULING_INDEX", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HX_FOULING_INDEX"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                HX_BASELINE_DP = new BaseDataVariableState(HX301)
                {
                    SymbolicName = "HX_BASELINE_DP",
                    NodeId = new NodeId("HX_BASELINE_DP", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("HX_BASELINE_DP", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "HX_BASELINE_DP"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.3,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Attach children
                HX301.AddChild(TT301);
                HX301.AddChild(TT302);
                HX301.AddChild(PT301);
                HX301.AddChild(FT301);
                HX301.AddChild(FT302);
                HX301.AddChild(FT303);
                HX301.AddChild(SV301);
                HX301.AddChild(P301_START);
                HX301.AddChild(P301_RUN);
                HX301.AddChild(P301_FAULT);
                HX301.AddChild(P301_SPEED);
                HX301.AddChild(HX301_STATE);
                HX301.AddChild(HX301_MODE);
                HX301.AddChild(hx_status);
                HX301.AddChild(heating_time);
                HX301.AddChild(hx_energy_consumed);
                HX301.AddChild(SP_HX_TEMP);
                HX301.AddChild(PID_HX_OUTPUT);
                HX301.AddChild(HX_FOULING_INDEX);
                HX301.AddChild(HX_BASELINE_DP);

                // Register with server
                AddPredefinedNode(SystemContext, HX301);

                // ************************ REACTOR NODES ************************
                RX401 = new FolderState(null)
                {
                    SymbolicName = "RX401",
                    NodeId = new NodeId("RX401", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("RX401", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "RX401"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                RX401.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, RX401.NodeId));

                // Temperatures (multi-point)
                TT401 = new BaseDataVariableState(RX401)
                {
                    SymbolicName = "TT401",
                    NodeId = new NodeId("TT401", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT401", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT401"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 25.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                TT401_1 = new BaseDataVariableState(RX401) { SymbolicName = "TT401_1", NodeId = new NodeId("TT401_1", NamespaceIndexes[1]), BrowseName = new QualifiedName("TT401_1", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "TT401_1"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 25.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                TT401_2 = new BaseDataVariableState(RX401) { SymbolicName = "TT401_2", NodeId = new NodeId("TT401_2", NamespaceIndexes[1]), BrowseName = new QualifiedName("TT401_2", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "TT401_2"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 25.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                TT401_3 = new BaseDataVariableState(RX401) { SymbolicName = "TT401_3", NodeId = new NodeId("TT401_3", NamespaceIndexes[1]), BrowseName = new QualifiedName("TT401_3", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "TT401_3"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 25.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                TT401_4 = new BaseDataVariableState(RX401) { SymbolicName = "TT401_4", NodeId = new NodeId("TT401_4", NamespaceIndexes[1]), BrowseName = new QualifiedName("TT401_4", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "TT401_4"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 25.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                TT401_5 = new BaseDataVariableState(RX401) { SymbolicName = "TT401_5", NodeId = new NodeId("TT401_5", NamespaceIndexes[1]), BrowseName = new QualifiedName("TT401_5", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "TT401_5"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 25.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Pressure / Level
                PT401 = new BaseDataVariableState(RX401) { SymbolicName = "PT401", NodeId = new NodeId("PT401", NamespaceIndexes[1]), BrowseName = new QualifiedName("PT401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "PT401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                LT401 = new BaseDataVariableState(RX401) { SymbolicName = "LT401", NodeId = new NodeId("LT401", NamespaceIndexes[1]), BrowseName = new QualifiedName("LT401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "LT401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Analytical instruments
                pH401 = new BaseDataVariableState(RX401) { SymbolicName = "pH401", NodeId = new NodeId("pH401", NamespaceIndexes[1]), BrowseName = new QualifiedName("pH401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "pH401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 7.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                BRIX401 = new BaseDataVariableState(RX401) { SymbolicName = "BRIX401", NodeId = new NodeId("BRIX401", NamespaceIndexes[1]), BrowseName = new QualifiedName("BRIX401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "BRIX401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                COLOR401_L = new BaseDataVariableState(RX401) { SymbolicName = "COLOR401_L", NodeId = new NodeId("COLOR401_L", NamespaceIndexes[1]), BrowseName = new QualifiedName("COLOR401_L", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "COLOR401_L"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 50.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                COLOR401_a = new BaseDataVariableState(RX401) { SymbolicName = "COLOR401_a", NodeId = new NodeId("COLOR401_a", NamespaceIndexes[1]), BrowseName = new QualifiedName("COLOR401_a", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "COLOR401_a"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                COLOR401_b = new BaseDataVariableState(RX401) { SymbolicName = "COLOR401_b", NodeId = new NodeId("COLOR401_b", NamespaceIndexes[1]), BrowseName = new QualifiedName("COLOR401_b", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "COLOR401_b"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                VISC401 = new BaseDataVariableState(RX401) { SymbolicName = "VISC401", NodeId = new NodeId("VISC401", NamespaceIndexes[1]), BrowseName = new QualifiedName("VISC401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "VISC401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Agitator motor
                MT401_START = new BaseDataVariableState(RX401) { SymbolicName = "MT401_START", NodeId = new NodeId("MT401_START", NamespaceIndexes[1]), BrowseName = new QualifiedName("MT401_START", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "MT401_START"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                MT401_RUN = new BaseDataVariableState(RX401) { SymbolicName = "MT401_RUN", NodeId = new NodeId("MT401_RUN", NamespaceIndexes[1]), BrowseName = new QualifiedName("MT401_RUN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "MT401_RUN"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                MT401_FAULT = new BaseDataVariableState(RX401) { SymbolicName = "MT401_FAULT", NodeId = new NodeId("MT401_FAULT", NamespaceIndexes[1]), BrowseName = new QualifiedName("MT401_FAULT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "MT401_FAULT"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                MT401_RPM = new BaseDataVariableState(RX401) { SymbolicName = "MT401_RPM", NodeId = new NodeId("MT401_RPM", NamespaceIndexes[1]), BrowseName = new QualifiedName("MT401_RPM", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "MT401_RPM"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                MT401_TRQ = new BaseDataVariableState(RX401) { SymbolicName = "MT401_TRQ", NodeId = new NodeId("MT401_TRQ", NamespaceIndexes[1]), BrowseName = new QualifiedName("MT401_TRQ", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "MT401_TRQ"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                MT401_PWR = new BaseDataVariableState(RX401) { SymbolicName = "MT401_PWR", NodeId = new NodeId("MT401_PWR", NamespaceIndexes[1]), BrowseName = new QualifiedName("MT401_PWR", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "MT401_PWR"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Vacuum system
                VP401_START = new BaseDataVariableState(RX401) { SymbolicName = "VP401_START", NodeId = new NodeId("VP401_START", NamespaceIndexes[1]), BrowseName = new QualifiedName("VP401_START", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "VP401_START"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                VP401_RUN = new BaseDataVariableState(RX401) { SymbolicName = "VP401_RUN", NodeId = new NodeId("VP401_RUN", NamespaceIndexes[1]), BrowseName = new QualifiedName("VP401_RUN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "VP401_RUN"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                VP401_FAULT = new BaseDataVariableState(RX401) { SymbolicName = "VP401_FAULT", NodeId = new NodeId("VP401_FAULT", NamespaceIndexes[1]), BrowseName = new QualifiedName("VP401_FAULT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "VP401_FAULT"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                PCV401 = new BaseDataVariableState(RX401) { SymbolicName = "PCV401", NodeId = new NodeId("PCV401", NamespaceIndexes[1]), BrowseName = new QualifiedName("PCV401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "PCV401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Jacket control
                SV401 = new BaseDataVariableState(RX401) { SymbolicName = "SV401", NodeId = new NodeId("SV401", NamespaceIndexes[1]), BrowseName = new QualifiedName("SV401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SV401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CV401 = new BaseDataVariableState(RX401) { SymbolicName = "CV401", NodeId = new NodeId("CV401", NamespaceIndexes[1]), BrowseName = new QualifiedName("CV401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CV401"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                TT401_JKT = new BaseDataVariableState(RX401) { SymbolicName = "TT401_JKT", NodeId = new NodeId("TT401_JKT", NamespaceIndexes[1]), BrowseName = new QualifiedName("TT401_JKT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "TT401_JKT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 25.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Acid dosing
                ACID_PUMP_START = new BaseDataVariableState(RX401) { SymbolicName = "ACID_PUMP_START", NodeId = new NodeId("ACID_PUMP_START", NamespaceIndexes[1]), BrowseName = new QualifiedName("ACID_PUMP_START", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "ACID_PUMP_START"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                ACID_PUMP_RUN = new BaseDataVariableState(RX401) { SymbolicName = "ACID_PUMP_RUN", NodeId = new NodeId("ACID_PUMP_RUN", NamespaceIndexes[1]), BrowseName = new QualifiedName("ACID_PUMP_RUN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "ACID_PUMP_RUN"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                ACID_FLOW = new BaseDataVariableState(RX401) { SymbolicName = "ACID_FLOW", NodeId = new NodeId("ACID_FLOW", NamespaceIndexes[1]), BrowseName = new QualifiedName("ACID_FLOW", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "ACID_FLOW"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                ACID_TOTAL = new BaseDataVariableState(RX401) { SymbolicName = "ACID_TOTAL", NodeId = new NodeId("ACID_TOTAL", NamespaceIndexes[1]), BrowseName = new QualifiedName("ACID_TOTAL", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "ACID_TOTAL"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Transfer valve
                VLV401 = new BaseDataVariableState(RX401) { SymbolicName = "VLV401", NodeId = new NodeId("VLV401", NamespaceIndexes[1]), BrowseName = new QualifiedName("VLV401", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "VLV401"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Reactor state/mode/status/energy
                RX401_STATE = new BaseDataVariableState(RX401) { SymbolicName = "RX401_STATE", NodeId = new NodeId("RX401_STATE", NamespaceIndexes[1]), BrowseName = new QualifiedName("RX401_STATE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "RX401_STATE"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 1, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                RX401_MODE = new BaseDataVariableState(RX401) { SymbolicName = "RX401_MODE", NodeId = new NodeId("RX401_MODE", NamespaceIndexes[1]), BrowseName = new QualifiedName("RX401_MODE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "RX401_MODE"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 1, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                reactor_status = new BaseDataVariableState(RX401) { SymbolicName = "reactor_status", NodeId = new NodeId("reactor_status", NamespaceIndexes[1]), BrowseName = new QualifiedName("reactor_status", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "reactor_status"), DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = "Idle", StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                reaction_time = new BaseDataVariableState(RX401) { SymbolicName = "reaction_time", NodeId = new NodeId("reaction_time", NamespaceIndexes[1]), BrowseName = new QualifiedName("reaction_time", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "reaction_time"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 1800, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                reactor_energy_consumed = new BaseDataVariableState(RX401) { SymbolicName = "reactor_energy_consumed", NodeId = new NodeId("reactor_energy_consumed", NamespaceIndexes[1]), BrowseName = new QualifiedName("reactor_energy_consumed", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "reactor_energy_consumed"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // PID setpoints
                SP_RX_TEMP = new BaseDataVariableState(RX401) { SymbolicName = "SP_RX_TEMP", NodeId = new NodeId("SP_RX_TEMP", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_RX_TEMP", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_RX_TEMP"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 88.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_RX_PH = new BaseDataVariableState(RX401) { SymbolicName = "SP_RX_PH", NodeId = new NodeId("SP_RX_PH", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_RX_PH", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_RX_PH"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 4.1, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_RX_VACUUM = new BaseDataVariableState(RX401) { SymbolicName = "SP_RX_VACUUM", NodeId = new NodeId("SP_RX_VACUUM", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_RX_VACUUM", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_RX_VACUUM"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = -0.2, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                PID_RX_OUTPUT = new BaseDataVariableState(RX401) { SymbolicName = "PID_RX_OUTPUT", NodeId = new NodeId("PID_RX_OUTPUT", NamespaceIndexes[1]), BrowseName = new QualifiedName("PID_RX_OUTPUT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "PID_RX_OUTPUT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Reaction phase tracking
                reaction_phase = new BaseDataVariableState(RX401) { SymbolicName = "reaction_phase", NodeId = new NodeId("reaction_phase", NamespaceIndexes[1]), BrowseName = new QualifiedName("reaction_phase", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "reaction_phase"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                phase_start_time = new BaseDataVariableState(RX401) { SymbolicName = "phase_start_time", NodeId = new NodeId("phase_start_time", NamespaceIndexes[1]), BrowseName = new QualifiedName("phase_start_time", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "phase_start_time"), DataType = DataTypeIds.DateTime, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = DateTime.Now, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                hold_time_remaining = new BaseDataVariableState(RX401) { SymbolicName = "hold_time_remaining", NodeId = new NodeId("hold_time_remaining", NamespaceIndexes[1]), BrowseName = new QualifiedName("hold_time_remaining", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "hold_time_remaining"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Quality metrics
                color_delta_e = new BaseDataVariableState(RX401) { SymbolicName = "color_delta_e", NodeId = new NodeId("color_delta_e", NamespaceIndexes[1]), BrowseName = new QualifiedName("color_delta_e", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "color_delta_e"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                batch_ph_ok = new BaseDataVariableState(RX401) { SymbolicName = "batch_ph_ok", NodeId = new NodeId("batch_ph_ok", NamespaceIndexes[1]), BrowseName = new QualifiedName("batch_ph_ok", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "batch_ph_ok"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Attach children
                RX401.AddChild(TT401);
                RX401.AddChild(TT401_1);
                RX401.AddChild(TT401_2);
                RX401.AddChild(TT401_3);
                RX401.AddChild(TT401_4);
                RX401.AddChild(TT401_5);
                RX401.AddChild(PT401);
                RX401.AddChild(LT401);
                RX401.AddChild(pH401);
                RX401.AddChild(BRIX401);
                RX401.AddChild(COLOR401_L);
                RX401.AddChild(COLOR401_a);
                RX401.AddChild(COLOR401_b);
                RX401.AddChild(VISC401);
                RX401.AddChild(MT401_START);
                RX401.AddChild(MT401_RUN);
                RX401.AddChild(MT401_FAULT);
                RX401.AddChild(MT401_RPM);
                RX401.AddChild(MT401_TRQ);
                RX401.AddChild(MT401_PWR);
                RX401.AddChild(VP401_START);
                RX401.AddChild(VP401_RUN);
                RX401.AddChild(VP401_FAULT);
                RX401.AddChild(PCV401);
                RX401.AddChild(SV401);
                RX401.AddChild(CV401);
                RX401.AddChild(TT401_JKT);
                RX401.AddChild(ACID_PUMP_START);
                RX401.AddChild(ACID_PUMP_RUN);
                RX401.AddChild(ACID_FLOW);
                RX401.AddChild(ACID_TOTAL);
                RX401.AddChild(VLV401);
                RX401.AddChild(RX401_STATE);
                RX401.AddChild(RX401_MODE);
                RX401.AddChild(reactor_status);
                RX401.AddChild(reaction_time);
                RX401.AddChild(reactor_energy_consumed);
                RX401.AddChild(SP_RX_TEMP);
                RX401.AddChild(SP_RX_PH);
                RX401.AddChild(SP_RX_VACUUM);
                RX401.AddChild(PID_RX_OUTPUT);
                RX401.AddChild(reaction_phase);
                RX401.AddChild(phase_start_time);
                RX401.AddChild(hold_time_remaining);
                RX401.AddChild(color_delta_e);
                RX401.AddChild(batch_ph_ok);

                // Register with server
                AddPredefinedNode(SystemContext, RX401);

                // ************************ FILLER NODES ************************
                FILLER501 = new FolderState(null)
                {
                    SymbolicName = "FILLER501",
                    NodeId = new NodeId("FILLER501", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("FILLER501", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "FILLER501"),
                    TypeDefinitionId = Opc.Ua.ObjectTypeIds.FolderType
                };

                FILLER501.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, FILLER501.NodeId));

                // Fill head temperature
                TT501 = new BaseDataVariableState(FILLER501)
                {
                    SymbolicName = "TT501",
                    NodeId = new NodeId("TT501", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("TT501", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "TT501"),
                    DataType = DataTypeIds.Double,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 25.0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                // Load cell / weighing
                LC501 = new BaseDataVariableState(FILLER501) { SymbolicName = "LC501", NodeId = new NodeId("LC501", NamespaceIndexes[1]), BrowseName = new QualifiedName("LC501", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "LC501"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                LC501_TARE = new BaseDataVariableState(FILLER501) { SymbolicName = "LC501_TARE", NodeId = new NodeId("LC501_TARE", NamespaceIndexes[1]), BrowseName = new QualifiedName("LC501_TARE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "LC501_TARE"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                LC501_NET = new BaseDataVariableState(FILLER501) { SymbolicName = "LC501_NET", NodeId = new NodeId("LC501_NET", NamespaceIndexes[1]), BrowseName = new QualifiedName("LC501_NET", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "LC501_NET"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Fill valves
                FV501_FAST = new BaseDataVariableState(FILLER501) { SymbolicName = "FV501_FAST", NodeId = new NodeId("FV501_FAST", NamespaceIndexes[1]), BrowseName = new QualifiedName("FV501_FAST", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FV501_FAST"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FV501_DRIBBLE = new BaseDataVariableState(FILLER501) { SymbolicName = "FV501_DRIBBLE", NodeId = new NodeId("FV501_DRIBBLE", NamespaceIndexes[1]), BrowseName = new QualifiedName("FV501_DRIBBLE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FV501_DRIBBLE"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Dosing pump
                P501_START = new BaseDataVariableState(FILLER501) { SymbolicName = "P501_START", NodeId = new NodeId("P501_START", NamespaceIndexes[1]), BrowseName = new QualifiedName("P501_START", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "P501_START"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                P501_RUN = new BaseDataVariableState(FILLER501) { SymbolicName = "P501_RUN", NodeId = new NodeId("P501_RUN", NamespaceIndexes[1]), BrowseName = new QualifiedName("P501_RUN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "P501_RUN"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                P501_FAULT = new BaseDataVariableState(FILLER501) { SymbolicName = "P501_FAULT", NodeId = new NodeId("P501_FAULT", NamespaceIndexes[1]), BrowseName = new QualifiedName("P501_FAULT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "P501_FAULT"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                P501_SPEED = new BaseDataVariableState(FILLER501) { SymbolicName = "P501_SPEED", NodeId = new NodeId("P501_SPEED", NamespaceIndexes[1]), BrowseName = new QualifiedName("P501_SPEED", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "P501_SPEED"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Conveyor
                CONV501_RUN = new BaseDataVariableState(FILLER501) { SymbolicName = "CONV501_RUN", NodeId = new NodeId("CONV501_RUN", NamespaceIndexes[1]), BrowseName = new QualifiedName("CONV501_RUN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CONV501_RUN"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CONV501_SPEED = new BaseDataVariableState(FILLER501) { SymbolicName = "CONV501_SPEED", NodeId = new NodeId("CONV501_SPEED", NamespaceIndexes[1]), BrowseName = new QualifiedName("CONV501_SPEED", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CONV501_SPEED"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CONV501_FAULT = new BaseDataVariableState(FILLER501) { SymbolicName = "CONV501_FAULT", NodeId = new NodeId("CONV501_FAULT", NamespaceIndexes[1]), BrowseName = new QualifiedName("CONV501_FAULT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CONV501_FAULT"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Capper
                CAP501_RUN = new BaseDataVariableState(FILLER501) { SymbolicName = "CAP501_RUN", NodeId = new NodeId("CAP501_RUN", NamespaceIndexes[1]), BrowseName = new QualifiedName("CAP501_RUN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CAP501_RUN"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CAP501_FAULT = new BaseDataVariableState(FILLER501) { SymbolicName = "CAP501_FAULT", NodeId = new NodeId("CAP501_FAULT", NamespaceIndexes[1]), BrowseName = new QualifiedName("CAP501_FAULT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CAP501_FAULT"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Filler state
                FL501_STATE = new BaseDataVariableState(FILLER501) { SymbolicName = "FL501_STATE", NodeId = new NodeId("FL501_STATE", NamespaceIndexes[1]), BrowseName = new QualifiedName("FL501_STATE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FL501_STATE"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 1, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FL501_MODE = new BaseDataVariableState(FILLER501) { SymbolicName = "FL501_MODE", NodeId = new NodeId("FL501_MODE", NamespaceIndexes[1]), BrowseName = new QualifiedName("FL501_MODE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FL501_MODE"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 1, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                filler_status = new BaseDataVariableState(FILLER501) { SymbolicName = "filler_status", NodeId = new NodeId("filler_status", NamespaceIndexes[1]), BrowseName = new QualifiedName("filler_status", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "filler_status"), DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = "Idle", StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                filler_energy_consumed = new BaseDataVariableState(FILLER501) { SymbolicName = "filler_energy_consumed", NodeId = new NodeId("filler_energy_consumed", NamespaceIndexes[1]), BrowseName = new QualifiedName("filler_energy_consumed", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "filler_energy_consumed"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Setpoints
                SP_FILL_WEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_FILL_WEIGHT", NodeId = new NodeId("SP_FILL_WEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_FILL_WEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_FILL_WEIGHT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 525.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_FILL_TEMP_MIN = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_FILL_TEMP_MIN", NodeId = new NodeId("SP_FILL_TEMP_MIN", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_FILL_TEMP_MIN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_FILL_TEMP_MIN"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 77.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_FILL_TEMP_MAX = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_FILL_TEMP_MAX", NodeId = new NodeId("SP_FILL_TEMP_MAX", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_FILL_TEMP_MAX", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_FILL_TEMP_MAX"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 84.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_JAR_VOLUME = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_JAR_VOLUME", NodeId = new NodeId("SP_JAR_VOLUME", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_JAR_VOLUME", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_JAR_VOLUME"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 500.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_FILL_RATE = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_FILL_RATE", NodeId = new NodeId("SP_FILL_RATE", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_FILL_RATE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_FILL_RATE"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 20.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Weight tolerances and inflight
                SP_WEIGHT_TOL_PCT = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_WEIGHT_TOL_PCT", NodeId = new NodeId("SP_WEIGHT_TOL_PCT", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_WEIGHT_TOL_PCT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_WEIGHT_TOL_PCT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 2.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_OVERWEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_OVERWEIGHT", NodeId = new NodeId("SP_OVERWEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_OVERWEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_OVERWEIGHT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 535.5, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                SP_UNDERWEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_UNDERWEIGHT", NodeId = new NodeId("SP_UNDERWEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_UNDERWEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_UNDERWEIGHT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 514.5, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                INFLIGHT_COMP = new BaseDataVariableState(FILLER501) { SymbolicName = "INFLIGHT_COMP", NodeId = new NodeId("INFLIGHT_COMP", NamespaceIndexes[1]), BrowseName = new QualifiedName("INFLIGHT_COMP", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "INFLIGHT_COMP"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 4.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Headspace
                SP_HEADSPACE = new BaseDataVariableState(FILLER501) { SymbolicName = "SP_HEADSPACE", NodeId = new NodeId("SP_HEADSPACE", NamespaceIndexes[1]), BrowseName = new QualifiedName("SP_HEADSPACE", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "SP_HEADSPACE"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 9.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                HEADSPACE_ACTUAL = new BaseDataVariableState(FILLER501) { SymbolicName = "HEADSPACE_ACTUAL", NodeId = new NodeId("HEADSPACE_ACTUAL", NamespaceIndexes[1]), BrowseName = new QualifiedName("HEADSPACE_ACTUAL", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "HEADSPACE_ACTUAL"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Counters
                CNT501_TOTAL = new BaseDataVariableState(FILLER501) { SymbolicName = "CNT501_TOTAL", NodeId = new NodeId("CNT501_TOTAL", NamespaceIndexes[1]), BrowseName = new QualifiedName("CNT501_TOTAL", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CNT501_TOTAL"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CNT501_PASS = new BaseDataVariableState(FILLER501) { SymbolicName = "CNT501_PASS", NodeId = new NodeId("CNT501_PASS", NamespaceIndexes[1]), BrowseName = new QualifiedName("CNT501_PASS", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CNT501_PASS"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CNT501_UNDERWEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "CNT501_UNDERWEIGHT", NodeId = new NodeId("CNT501_UNDERWEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("CNT501_UNDERWEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CNT501_UNDERWEIGHT"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CNT501_OVERWEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "CNT501_OVERWEIGHT", NodeId = new NodeId("CNT501_OVERWEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("CNT501_OVERWEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CNT501_OVERWEIGHT"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CNT501_TEMP_REJECT = new BaseDataVariableState(FILLER501) { SymbolicName = "CNT501_TEMP_REJECT", NodeId = new NodeId("CNT501_TEMP_REJECT", NamespaceIndexes[1]), BrowseName = new QualifiedName("CNT501_TEMP_REJECT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CNT501_TEMP_REJECT"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Current jar data
                CURRENT_JAR_WEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "CURRENT_JAR_WEIGHT", NodeId = new NodeId("CURRENT_JAR_WEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("CURRENT_JAR_WEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CURRENT_JAR_WEIGHT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CURRENT_JAR_TEMP = new BaseDataVariableState(FILLER501) { SymbolicName = "CURRENT_JAR_TEMP", NodeId = new NodeId("CURRENT_JAR_TEMP", NamespaceIndexes[1]), BrowseName = new QualifiedName("CURRENT_JAR_TEMP", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CURRENT_JAR_TEMP"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                CURRENT_JAR_STATUS = new BaseDataVariableState(FILLER501) { SymbolicName = "CURRENT_JAR_STATUS", NodeId = new NodeId("CURRENT_JAR_STATUS", NamespaceIndexes[1]), BrowseName = new QualifiedName("CURRENT_JAR_STATUS", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "CURRENT_JAR_STATUS"), DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = "", StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                PREVIOUS_JAR_WEIGHT = new BaseDataVariableState(FILLER501) { SymbolicName = "PREVIOUS_JAR_WEIGHT", NodeId = new NodeId("PREVIOUS_JAR_WEIGHT", NamespaceIndexes[1]), BrowseName = new QualifiedName("PREVIOUS_JAR_WEIGHT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "PREVIOUS_JAR_WEIGHT"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Fill statistics
                FILL_WEIGHT_AVG = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_WEIGHT_AVG", NodeId = new NodeId("FILL_WEIGHT_AVG", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_WEIGHT_AVG", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_WEIGHT_AVG"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FILL_WEIGHT_MIN = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_WEIGHT_MIN", NodeId = new NodeId("FILL_WEIGHT_MIN", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_WEIGHT_MIN", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_WEIGHT_MIN"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FILL_WEIGHT_MAX = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_WEIGHT_MAX", NodeId = new NodeId("FILL_WEIGHT_MAX", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_WEIGHT_MAX", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_WEIGHT_MAX"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FILL_WEIGHT_STDDEV = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_WEIGHT_STDDEV", NodeId = new NodeId("FILL_WEIGHT_STDDEV", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_WEIGHT_STDDEV", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_WEIGHT_STDDEV"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FIRST_PASS_YIELD = new BaseDataVariableState(FILLER501) { SymbolicName = "FIRST_PASS_YIELD", NodeId = new NodeId("FIRST_PASS_YIELD", NamespaceIndexes[1]), BrowseName = new QualifiedName("FIRST_PASS_YIELD", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FIRST_PASS_YIELD"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                GIVEAWAY_TOTAL = new BaseDataVariableState(FILLER501) { SymbolicName = "GIVEAWAY_TOTAL", NodeId = new NodeId("GIVEAWAY_TOTAL", NamespaceIndexes[1]), BrowseName = new QualifiedName("GIVEAWAY_TOTAL", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "GIVEAWAY_TOTAL"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Batch tracking
                JARS_REMAINING = new BaseDataVariableState(FILLER501) { SymbolicName = "JARS_REMAINING", NodeId = new NodeId("JARS_REMAINING", NamespaceIndexes[1]), BrowseName = new QualifiedName("JARS_REMAINING", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "JARS_REMAINING"), DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FILL_START_TIME = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_START_TIME", NodeId = new NodeId("FILL_START_TIME", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_START_TIME", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_START_TIME"), DataType = DataTypeIds.DateTime, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = DateTime.Now, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FILL_END_TIME = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_END_TIME", NodeId = new NodeId("FILL_END_TIME", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_END_TIME", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_END_TIME"), DataType = DataTypeIds.DateTime, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = DateTime.Now, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                FILL_ELAPSED_TIME = new BaseDataVariableState(FILLER501) { SymbolicName = "FILL_ELAPSED_TIME", NodeId = new NodeId("FILL_ELAPSED_TIME", NamespaceIndexes[1]), BrowseName = new QualifiedName("FILL_ELAPSED_TIME", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "FILL_ELAPSED_TIME"), DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = 0.0, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Indicators
                IND_FILLING = new BaseDataVariableState(FILLER501) { SymbolicName = "IND_FILLING", NodeId = new NodeId("IND_FILLING", NamespaceIndexes[1]), BrowseName = new QualifiedName("IND_FILLING", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "IND_FILLING"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                IND_JAR_PRESENT = new BaseDataVariableState(FILLER501) { SymbolicName = "IND_JAR_PRESENT", NodeId = new NodeId("IND_JAR_PRESENT", NamespaceIndexes[1]), BrowseName = new QualifiedName("IND_JAR_PRESENT", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "IND_JAR_PRESENT"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                IND_WEIGHT_OK = new BaseDataVariableState(FILLER501) { SymbolicName = "IND_WEIGHT_OK", NodeId = new NodeId("IND_WEIGHT_OK", NamespaceIndexes[1]), BrowseName = new QualifiedName("IND_WEIGHT_OK", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "IND_WEIGHT_OK"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };
                IND_TEMP_OK = new BaseDataVariableState(FILLER501) { SymbolicName = "IND_TEMP_OK", NodeId = new NodeId("IND_TEMP_OK", NamespaceIndexes[1]), BrowseName = new QualifiedName("IND_TEMP_OK", NamespaceIndexes[1]), DisplayName = new LocalizedText("en", "IND_TEMP_OK"), DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite, Value = false, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow };

                // Attach children
                FILLER501.AddChild(TT501);
                FILLER501.AddChild(LC501);
                FILLER501.AddChild(LC501_TARE);
                FILLER501.AddChild(LC501_NET);
                FILLER501.AddChild(FV501_FAST);
                FILLER501.AddChild(FV501_DRIBBLE);
                FILLER501.AddChild(P501_START);
                FILLER501.AddChild(P501_RUN);
                FILLER501.AddChild(P501_FAULT);
                FILLER501.AddChild(P501_SPEED);
                FILLER501.AddChild(CONV501_RUN);
                FILLER501.AddChild(CONV501_SPEED);
                FILLER501.AddChild(CONV501_FAULT);
                FILLER501.AddChild(CAP501_RUN);
                FILLER501.AddChild(CAP501_FAULT);
                FILLER501.AddChild(FL501_STATE);
                FILLER501.AddChild(FL501_MODE);
                FILLER501.AddChild(filler_status);
                FILLER501.AddChild(filler_energy_consumed);
                FILLER501.AddChild(SP_FILL_WEIGHT);
                FILLER501.AddChild(SP_FILL_TEMP_MIN);
                FILLER501.AddChild(SP_FILL_TEMP_MAX);
                FILLER501.AddChild(SP_JAR_VOLUME);
                FILLER501.AddChild(SP_FILL_RATE);
                FILLER501.AddChild(SP_WEIGHT_TOL_PCT);
                FILLER501.AddChild(SP_OVERWEIGHT);
                FILLER501.AddChild(SP_UNDERWEIGHT);
                FILLER501.AddChild(INFLIGHT_COMP);
                FILLER501.AddChild(SP_HEADSPACE);
                FILLER501.AddChild(HEADSPACE_ACTUAL);
                FILLER501.AddChild(CNT501_TOTAL);
                FILLER501.AddChild(CNT501_PASS);
                FILLER501.AddChild(CNT501_UNDERWEIGHT);
                FILLER501.AddChild(CNT501_OVERWEIGHT);
                FILLER501.AddChild(CNT501_TEMP_REJECT);
                FILLER501.AddChild(CURRENT_JAR_WEIGHT);
                FILLER501.AddChild(CURRENT_JAR_TEMP);
                FILLER501.AddChild(CURRENT_JAR_STATUS);
                FILLER501.AddChild(PREVIOUS_JAR_WEIGHT);
                FILLER501.AddChild(FILL_WEIGHT_AVG);
                FILLER501.AddChild(FILL_WEIGHT_MIN);
                FILLER501.AddChild(FILL_WEIGHT_MAX);
                FILLER501.AddChild(FILL_WEIGHT_STDDEV);
                FILLER501.AddChild(FIRST_PASS_YIELD);
                FILLER501.AddChild(GIVEAWAY_TOTAL);
                FILLER501.AddChild(JARS_REMAINING);
                FILLER501.AddChild(FILL_START_TIME);
                FILLER501.AddChild(FILL_END_TIME);
                FILLER501.AddChild(FILL_ELAPSED_TIME);
                FILLER501.AddChild(IND_FILLING);
                FILLER501.AddChild(IND_JAR_PRESENT);
                FILLER501.AddChild(IND_WEIGHT_OK);
                FILLER501.AddChild(IND_TEMP_OK);

                // Register with server
                AddPredefinedNode(SystemContext, FILLER501);


                // --- StartProcess method under Objects ---
                var startProcess = new MethodState(null)
                {
                    SymbolicName = "StartProcess",
                    NodeId = new NodeId("StartProcess", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("StartProcess", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "StartProcess"),
                    Executable = true,
                    UserExecutable = true
                };

                
                startProcess.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, startProcess.NodeId));

                // Wire the handler
                startProcess.OnCallMethod =
                    new GenericMethodCalledEventHandler(OnStartProcess);

                // Register with server
                AddPredefinedNode(SystemContext, startProcess);


                // --- StopProcess method under Objects ---
                var stopProcess = new MethodState(null)
                {
                    SymbolicName = "StopProcess",
                    NodeId = new NodeId("StopProcess", NamespaceIndexes[1]),
                    BrowseName = new QualifiedName("StopProcess", NamespaceIndexes[1]),
                    DisplayName = new LocalizedText("en", "StopProcess"),
                    Executable = true,
                    UserExecutable = true
                };

                stopProcess.AddReference(ReferenceTypeIds.Organizes, true, Opc.Ua.ObjectIds.ObjectsFolder);
                objectsFolderRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, stopProcess.NodeId));

                stopProcess.OnCallMethod =
                    new GenericMethodCalledEventHandler(OnStopProcess);

                AddPredefinedNode(SystemContext, stopProcess);


                // ═══════════════════════════════════════════════════════════════════
                // INITIALIZE OPC UA ALARMS & CONDITIONS (Part 9)
                // ═══════════════════════════════════════════════════════════════════

                AlarmManager = new AlarmConditionManager(Server, NamespaceIndexes[1]);
                var alarmsFolder = AlarmManager.Initialize(SystemContext, objectsFolderRefs);
                AddPredefinedNode(SystemContext, alarmsFolder);
                Console.WriteLine("[OK] OPC UA Alarm Conditions initialized");
            }
        }


        private ServiceResult OnStartProcess(ISystemContext context, MethodState method, IList<object> inputArguments,
    IList<object> outputArguments)
        {

            return ServiceResult.Good;
        }

        private ServiceResult OnStopProcess(ISystemContext context, MethodState method, IList<object> inputArguments,
IList<object> outputArguments)
        {

            return ServiceResult.Good;
        }

        //RAW MATERIAL TANKS
        public static FolderState TK101;
        public static BaseDataVariableState LT101;
        public static BaseDataVariableState FCV101;
        public static BaseDataVariableState P101_START;
        public static BaseDataVariableState P101_RUN;
        public static BaseDataVariableState P101_FAULT;
        public static BaseDataVariableState TT101;  //temperature
        public static BaseDataVariableState PT101; //headspace pressure

        public static FolderState TK102;
        public static BaseDataVariableState LT102;
        public static BaseDataVariableState FCV102;
        public static BaseDataVariableState P102_START;
        public static BaseDataVariableState P102_RUN;
        public static BaseDataVariableState P102_FAULT;
        public static BaseDataVariableState TT102;
        public static BaseDataVariableState PT102;

        public static FolderState TK103;
        public static BaseDataVariableState LT103;
        public static BaseDataVariableState FCV103;
        public static BaseDataVariableState P103_START;
        public static BaseDataVariableState P103_RUN;
        public static BaseDataVariableState P103_FAULT;
        public static BaseDataVariableState TT103;
        public static BaseDataVariableState PT103;

        //BATCH CONTROL 
        public static FolderState batchControl;
        public static BaseDataVariableState BATCH_START;
        public static BaseDataVariableState BATCH_PAUSE;
        public static BaseDataVariableState BATCH_ABORT;


        //BATCH PRODUCTION 
        public static FolderState batchProduction;
        public static BaseDataVariableState batch_code;
        public static BaseDataVariableState product_code;
        public static BaseDataVariableState batch_status;
        public static BaseDataVariableState batch_start_time;
        public static BaseDataVariableState batch_end_time;
        public static BaseDataVariableState batches_required;
        public static BaseDataVariableState batches_done;
        public static BaseDataVariableState material1_target;
        public static BaseDataVariableState material2_target;
        public static BaseDataVariableState material3_target;
        public static BaseDataVariableState material1_actual;
        public static BaseDataVariableState material2_actual;
        public static BaseDataVariableState material3_actual;
        public static BaseDataVariableState material1_name;
        public static BaseDataVariableState material2_name;
        public static BaseDataVariableState material3_name;


        //MIXER 
        public static FolderState MX101;
        public static BaseDataVariableState TT201;
        public static BaseDataVariableState LT201;
        public static BaseDataVariableState VISC201;
        public static BaseDataVariableState TURB201;
        public static BaseDataVariableState MT201_START;
        public static BaseDataVariableState MT201_RUN;
        public static BaseDataVariableState MT201_FAULT;
        public static BaseDataVariableState MT201_RPM;
        public static BaseDataVariableState MT201_TRQ;
        public static BaseDataVariableState MT201_PWR;
        public static BaseDataVariableState MT201_AMP;
        public static BaseDataVariableState MT201_VIB;
        public static BaseDataVariableState MX101_STATE;
        public static BaseDataVariableState MX101_MODE;
        public static BaseDataVariableState mixer_status;
        public static BaseDataVariableState mixing_time;
        public static BaseDataVariableState mixing_speed;
        public static BaseDataVariableState mixer_energy_consumed;
        public static BaseDataVariableState SP_MX_INITIAL_SPEED;
        public static BaseDataVariableState SP_MX_FINAL_SPEED;
        public static BaseDataVariableState SP_MX_AMBIENT_TEMP;
        public static BaseDataVariableState SP_MX_INITIAL_VISC;
        public static BaseDataVariableState SP_MX_TARGET_VISC;

        // Mixer Report Parameters
        public static FolderState MX101_ReportParameters;
        public static BaseDataVariableState MX_RPT_AVG_AGITATOR_SPEED;
        public static BaseDataVariableState MX_RPT_AVG_MOTOR_POWER;
        public static BaseDataVariableState MX_RPT_FINAL_HOMOGENEITY;
        public static BaseDataVariableState MX_RPT_FINAL_TEMPERATURE;
        public static BaseDataVariableState MX_RPT_FINAL_TURBIDITY;
        public static BaseDataVariableState MX_RPT_FINAL_VISCOSITY;
        public static BaseDataVariableState MX_RPT_INITIAL_VISCOSITY;
        public static BaseDataVariableState MX_RPT_MAX_MOTOR_POWER;
        public static BaseDataVariableState MX_RPT_MIX_DURATION;


        // ************************ HEAT EXCHANGER FIELD DECLARATIONS ************************
        public static FolderState HX301;
        public static BaseDataVariableState TT301;
        public static BaseDataVariableState TT302;
        public static BaseDataVariableState PT301;
        public static BaseDataVariableState FT301;
        public static BaseDataVariableState FT302;
        public static BaseDataVariableState FT303;
        public static BaseDataVariableState SV301;
        public static BaseDataVariableState P301_START;
        public static BaseDataVariableState P301_RUN;
        public static BaseDataVariableState P301_FAULT;
        public static BaseDataVariableState P301_SPEED;
        public static BaseDataVariableState HX301_STATE;
        public static BaseDataVariableState HX301_MODE;
        public static BaseDataVariableState hx_status;
        public static BaseDataVariableState heating_time;
        public static BaseDataVariableState hx_energy_consumed;
        public static BaseDataVariableState SP_HX_TEMP;
        public static BaseDataVariableState PID_HX_OUTPUT;
        public static BaseDataVariableState HX_FOULING_INDEX;
        public static BaseDataVariableState HX_BASELINE_DP;


        // ************************ REACTOR FIELD DECLARATIONS ************************
        public static FolderState RX401;
        public static BaseDataVariableState TT401;
        public static BaseDataVariableState TT401_1;
        public static BaseDataVariableState TT401_2;
        public static BaseDataVariableState TT401_3;
        public static BaseDataVariableState TT401_4;
        public static BaseDataVariableState TT401_5;
        public static BaseDataVariableState PT401;
        public static BaseDataVariableState LT401;
        public static BaseDataVariableState pH401;
        public static BaseDataVariableState BRIX401;
        public static BaseDataVariableState COLOR401_L;
        public static BaseDataVariableState COLOR401_a;
        public static BaseDataVariableState COLOR401_b;
        public static BaseDataVariableState VISC401;
        public static BaseDataVariableState MT401_START;
        public static BaseDataVariableState MT401_RUN;
        public static BaseDataVariableState MT401_FAULT;
        public static BaseDataVariableState MT401_RPM;
        public static BaseDataVariableState MT401_TRQ;
        public static BaseDataVariableState MT401_PWR;
        public static BaseDataVariableState VP401_START;
        public static BaseDataVariableState VP401_RUN;
        public static BaseDataVariableState VP401_FAULT;
        public static BaseDataVariableState PCV401;
        public static BaseDataVariableState SV401;
        public static BaseDataVariableState CV401;
        public static BaseDataVariableState TT401_JKT;
        public static BaseDataVariableState ACID_PUMP_START;
        public static BaseDataVariableState ACID_PUMP_RUN;
        public static BaseDataVariableState ACID_FLOW;
        public static BaseDataVariableState ACID_TOTAL;
        public static BaseDataVariableState VLV401;
        public static BaseDataVariableState RX401_STATE;
        public static BaseDataVariableState RX401_MODE;
        public static BaseDataVariableState reactor_status;
        public static BaseDataVariableState reaction_time;
        public static BaseDataVariableState reactor_energy_consumed;
        public static BaseDataVariableState SP_RX_TEMP;
        public static BaseDataVariableState SP_RX_PH;
        public static BaseDataVariableState SP_RX_VACUUM;
        public static BaseDataVariableState PID_RX_OUTPUT;
        public static BaseDataVariableState reaction_phase;
        public static BaseDataVariableState phase_start_time;
        public static BaseDataVariableState hold_time_remaining;
        public static BaseDataVariableState color_delta_e;
        public static BaseDataVariableState batch_ph_ok;

        // ************************ FILLER FIELD DECLARATIONS ************************
        public static FolderState FILLER501;
        public static BaseDataVariableState TT501;
        public static BaseDataVariableState LC501;
        public static BaseDataVariableState LC501_TARE;
        public static BaseDataVariableState LC501_NET;
        public static BaseDataVariableState FV501_FAST;
        public static BaseDataVariableState FV501_DRIBBLE;
        public static BaseDataVariableState P501_START;
        public static BaseDataVariableState P501_RUN;
        public static BaseDataVariableState P501_FAULT;
        public static BaseDataVariableState P501_SPEED;
        public static BaseDataVariableState CONV501_RUN;
        public static BaseDataVariableState CONV501_SPEED;
        public static BaseDataVariableState CONV501_FAULT;
        public static BaseDataVariableState CAP501_RUN;
        public static BaseDataVariableState CAP501_FAULT;
        public static BaseDataVariableState FL501_STATE;
        public static BaseDataVariableState FL501_MODE;
        public static BaseDataVariableState filler_status;
        public static BaseDataVariableState filler_energy_consumed;
        public static BaseDataVariableState SP_FILL_WEIGHT;
        public static BaseDataVariableState SP_FILL_TEMP_MIN;
        public static BaseDataVariableState SP_FILL_TEMP_MAX;
        public static BaseDataVariableState SP_JAR_VOLUME;
        public static BaseDataVariableState SP_FILL_RATE;
        public static BaseDataVariableState SP_WEIGHT_TOL_PCT;
        public static BaseDataVariableState SP_OVERWEIGHT;
        public static BaseDataVariableState SP_UNDERWEIGHT;
        public static BaseDataVariableState INFLIGHT_COMP;
        public static BaseDataVariableState SP_HEADSPACE;
        public static BaseDataVariableState HEADSPACE_ACTUAL;
        public static BaseDataVariableState CNT501_TOTAL;
        public static BaseDataVariableState CNT501_PASS;
        public static BaseDataVariableState CNT501_UNDERWEIGHT;
        public static BaseDataVariableState CNT501_OVERWEIGHT;
        public static BaseDataVariableState CNT501_TEMP_REJECT;
        public static BaseDataVariableState CURRENT_JAR_WEIGHT;
        public static BaseDataVariableState CURRENT_JAR_TEMP;
        public static BaseDataVariableState CURRENT_JAR_STATUS;
        public static BaseDataVariableState PREVIOUS_JAR_WEIGHT;
        public static BaseDataVariableState FILL_WEIGHT_AVG;
        public static BaseDataVariableState FILL_WEIGHT_MIN;
        public static BaseDataVariableState FILL_WEIGHT_MAX;
        public static BaseDataVariableState FILL_WEIGHT_STDDEV;
        public static BaseDataVariableState FIRST_PASS_YIELD;
        public static BaseDataVariableState GIVEAWAY_TOTAL;
        public static BaseDataVariableState JARS_REMAINING;
        public static BaseDataVariableState FILL_START_TIME;
        public static BaseDataVariableState FILL_END_TIME;
        public static BaseDataVariableState FILL_ELAPSED_TIME;
        public static BaseDataVariableState IND_FILLING;
        public static BaseDataVariableState IND_JAR_PRESENT;
        public static BaseDataVariableState IND_WEIGHT_OK;
        public static BaseDataVariableState IND_TEMP_OK;



        public BatchPlantServerConfiguration m_configuration;
      

    }
}
