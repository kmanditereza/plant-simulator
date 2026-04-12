using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Concurrent;

namespace BatchPlant.Alarms
{
    /// <summary>
    /// Represents the runtime state of an alarm condition.
    /// </summary>
    public class AlarmState
    {
        public AlarmDefinition Definition { get; set; } = null!;
        public ExclusiveLimitAlarmState? AlarmNode { get; set; }
        public bool IsActive { get; set; }
        public bool IsAcknowledged { get; set; }
        public bool IsConfirmed { get; set; }
        public bool IsSuppressed { get; set; }
        public DateTime? ActiveTime { get; set; }
        public DateTime? AcknowledgeTime { get; set; }
        public DateTime? ConfirmTime { get; set; }
        public double LastValue { get; set; }
        public double? TriggerValue { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public NodeId? SourceNodeId { get; set; }

        // For on-delay timing
        public DateTime? ConditionMetTime { get; set; }
        public bool DelayActive { get; set; }
    }

    /// <summary>
    /// Manages OPC UA Alarm Conditions (Part 9) for the Batch Plant.
    /// Creates alarm nodes, monitors conditions, and raises events.
    /// </summary>
    public class AlarmConditionManager
    {
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<string, AlarmState> _alarms = new();
        private readonly IServerInternal _server;
        private readonly ushort _namespaceIndex;

        // Folders for organizing alarms
        private FolderState? _alarmsFolder;
        private FolderState? _mixerAlarmsFolder;
        private FolderState? _heatExchangerAlarmsFolder;
        private FolderState? _reactorAlarmsFolder;

        // Event counters
        private uint _eventIdCounter = 0;

        /// <summary>
        /// Gets the singleton instance of the AlarmConditionManager.
        /// </summary>
        public static AlarmConditionManager? Instance { get; private set; }

        /// <summary>
        /// Creates a new AlarmConditionManager.
        /// </summary>
        /// <param name="server">The OPC UA server instance.</param>
        /// <param name="namespaceIndex">Namespace index for alarm nodes.</param>
        public AlarmConditionManager(IServerInternal server, ushort namespaceIndex)
        {
            _server = server;
            _namespaceIndex = namespaceIndex;
            Instance = this;
        }

        /// <summary>
        /// Initializes the alarm system and creates all alarm nodes in the address space.
        /// Call this at the end of CreateAddressSpace.
        /// </summary>
        /// <param name="context">The system context.</param>
        /// <param name="externalReferences">External references from CreateAddressSpace.</param>
        /// <returns>The root Alarms folder to be added to the node manager.</returns>
        public FolderState Initialize(ISystemContext context, IList<IReference> externalReferences)
        {
            Console.WriteLine("[...] Initializing OPC UA Alarm Conditions (Part 9)...");

            // Create main Alarms folder under Objects
            _alarmsFolder = CreateFolder(context, null, "Alarms", "Alarms");
            _alarmsFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
            externalReferences.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, _alarmsFolder.NodeId));

            // Enable event notifier on the alarms folder
            _alarmsFolder.EventNotifier = EventNotifiers.SubscribeToEvents;

            // Create sub-folders for each area
            _mixerAlarmsFolder = CreateFolder(context, _alarmsFolder, "MixerAlarms", "Mixer Alarms (Stage 1)");
            _mixerAlarmsFolder.EventNotifier = EventNotifiers.SubscribeToEvents;
            _alarmsFolder.AddChild(_mixerAlarmsFolder);

            _heatExchangerAlarmsFolder = CreateFolder(context, _alarmsFolder, "HeatExchangerAlarms", "Heat Exchanger Alarms (Stage 2)");
            _heatExchangerAlarmsFolder.EventNotifier = EventNotifiers.SubscribeToEvents;
            _alarmsFolder.AddChild(_heatExchangerAlarmsFolder);

            _reactorAlarmsFolder = CreateFolder(context, _alarmsFolder, "ReactorAlarms", "Reactor Alarms (Stage 3)");
            _reactorAlarmsFolder.EventNotifier = EventNotifiers.SubscribeToEvents;
            _alarmsFolder.AddChild(_reactorAlarmsFolder);

            // Create alarm condition nodes for each defined alarm
            foreach (var alarmDef in PlantAlarms.GetMixerAlarms())
            {
                CreateAlarmCondition(context, alarmDef, _mixerAlarmsFolder);
            }

            foreach (var alarmDef in PlantAlarms.GetHeatExchangerAlarms())
            {
                CreateAlarmCondition(context, alarmDef, _heatExchangerAlarmsFolder);
            }

            foreach (var alarmDef in PlantAlarms.GetReactorAlarms())
            {
                CreateAlarmCondition(context, alarmDef, _reactorAlarmsFolder);
            }

            Console.WriteLine($"[OK] {_alarms.Count} OPC UA Alarm Conditions created");
            Console.WriteLine($"     Browse: Objects/Alarms/[MixerAlarms|HeatExchangerAlarms|ReactorAlarms]");

            // Return the folder so the caller can add it to the node manager
            return _alarmsFolder;
        }

        /// <summary>
        /// Creates a folder node.
        /// </summary>
        private FolderState CreateFolder(ISystemContext context, NodeState? parent, string name, string displayName)
        {
            var folder = new FolderState(parent)
            {
                SymbolicName = name,
                NodeId = new NodeId(name, _namespaceIndex),
                BrowseName = new QualifiedName(name, _namespaceIndex),
                DisplayName = new LocalizedText("en", displayName),
                TypeDefinitionId = ObjectTypeIds.FolderType,
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None
            };

            if (parent != null)
            {
                folder.AddReference(ReferenceTypeIds.HasNotifier, true, parent.NodeId);
            }

            return folder;
        }

        /// <summary>
        /// Creates an alarm condition node for the given definition.
        /// </summary>
        private void CreateAlarmCondition(ISystemContext context, AlarmDefinition definition, FolderState parentFolder)
        {
            try
            {
                // Create the alarm condition state
                var alarm = new ExclusiveLimitAlarmState(parentFolder);

                string nodeIdString = $"Alarm.{definition.AlarmId}";

                // Initialize the alarm with basic properties
                alarm.SymbolicName = definition.AlarmId;
                alarm.NodeId = new NodeId(nodeIdString, _namespaceIndex);
                alarm.BrowseName = new QualifiedName(definition.AlarmId, _namespaceIndex);
                alarm.DisplayName = new LocalizedText("en", definition.Name);
                alarm.TypeDefinitionId = ObjectTypeIds.ExclusiveLimitAlarmType;

                // Create with proper initialization
                alarm.Create(
                    context,
                    alarm.NodeId,
                    alarm.BrowseName,
                    alarm.DisplayName,
                    true);

                // Set source information
                alarm.SourceName = new PropertyState<string>(alarm)
                {
                    NodeId = new NodeId($"{nodeIdString}.SourceName", _namespaceIndex),
                    BrowseName = BrowseNames.SourceName,
                    DisplayName = "SourceName",
                    TypeDefinitionId = VariableTypeIds.PropertyType,
                    ReferenceTypeId = ReferenceTypeIds.HasProperty,
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    Value = definition.Unit
                };

                alarm.SourceNode = new PropertyState<NodeId>(alarm)
                {
                    NodeId = new NodeId($"{nodeIdString}.SourceNode", _namespaceIndex),
                    BrowseName = BrowseNames.SourceNode,
                    DisplayName = "SourceNode",
                    TypeDefinitionId = VariableTypeIds.PropertyType,
                    ReferenceTypeId = ReferenceTypeIds.HasProperty,
                    DataType = DataTypeIds.NodeId,
                    ValueRank = ValueRanks.Scalar,
                    Value = ObjectIds.Server
                };

                // Set condition name
                if (alarm.ConditionName != null)
                {
                    alarm.ConditionName.Value = definition.Name;
                }

                // Set message
                if (alarm.Message != null)
                {
                    alarm.Message.Value = new LocalizedText("en", definition.Description);
                }

                // Set severity
                if (alarm.Severity != null)
                {
                    alarm.Severity.Value = definition.Severity;
                }

                // Set enabled state
                if (alarm.EnabledState != null)
                {
                    alarm.EnabledState.Value = new LocalizedText("en", definition.Enabled ? "Enabled" : "Disabled");
                    if (alarm.EnabledState.Id != null)
                    {
                        alarm.EnabledState.Id.Value = definition.Enabled;
                    }
                }

                // Set limit values
                if (definition.HighHighLimit.HasValue && alarm.HighHighLimit != null)
                {
                    alarm.HighHighLimit.Value = definition.HighHighLimit.Value;
                }

                if (definition.HighLimit.HasValue && alarm.HighLimit != null)
                {
                    alarm.HighLimit.Value = definition.HighLimit.Value;
                }

                if (definition.LowLimit.HasValue && alarm.LowLimit != null)
                {
                    alarm.LowLimit.Value = definition.LowLimit.Value;
                }

                if (definition.LowLowLimit.HasValue && alarm.LowLowLimit != null)
                {
                    alarm.LowLowLimit.Value = definition.LowLowLimit.Value;
                }

                // Initialize active state (inactive)
                if (alarm.ActiveState != null)
                {
                    alarm.ActiveState.Value = new LocalizedText("en", "Inactive");
                    if (alarm.ActiveState.Id != null)
                    {
                        alarm.ActiveState.Id.Value = false;
                    }
                    if (alarm.ActiveState.EffectiveDisplayName != null)
                    {
                        alarm.ActiveState.EffectiveDisplayName.Value = new LocalizedText("en", "Inactive");
                    }
                }

                // Initialize acknowledged state (acknowledged when inactive)
                if (alarm.AckedState != null)
                {
                    alarm.AckedState.Value = new LocalizedText("en", "Acknowledged");
                    if (alarm.AckedState.Id != null)
                    {
                        alarm.AckedState.Id.Value = true;
                    }
                }

                // Initialize confirmed state
                if (alarm.ConfirmedState != null)
                {
                    alarm.ConfirmedState.Value = new LocalizedText("en", "Confirmed");
                    if (alarm.ConfirmedState.Id != null)
                    {
                        alarm.ConfirmedState.Id.Value = true;
                    }
                }

                // Set retain to false initially
                if (alarm.Retain != null)
                {
                    alarm.Retain.Value = false;
                }

                // Set up method handlers for Acknowledge and Confirm
                alarm.OnAcknowledge = OnAcknowledgeAlarm;
                alarm.OnConfirm = OnConfirmAlarm;

                // Add reference from parent folder
                parentFolder.AddChild(alarm);

                // Add HasNotifier reference for event propagation
                alarm.AddReference(ReferenceTypeIds.HasNotifier, true, parentFolder.NodeId);

                // Create alarm state tracker
                var alarmState = new AlarmState
                {
                    Definition = definition,
                    AlarmNode = alarm,
                    IsActive = false,
                    IsAcknowledged = true,
                    IsConfirmed = !definition.RequiresConfirmation
                };

                _alarms[definition.AlarmId] = alarmState;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create alarm {definition.AlarmId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks an alarm condition against a value and triggers/clears as needed.
        /// </summary>
        /// <param name="alarmId">The alarm ID to check.</param>
        /// <param name="currentValue">The current value of the monitored variable.</param>
        /// <param name="setpoint">Optional setpoint for deviation alarms.</param>
        /// <returns>True if alarm is now active, false otherwise.</returns>
        public bool CheckAlarm(string alarmId, double currentValue, double? setpoint = null)
        {
            if (!_alarms.TryGetValue(alarmId, out var alarmState))
            {
                return false;
            }

            var def = alarmState.Definition;
            if (!def.Enabled)
            {
                return false;
            }

            alarmState.LastValue = currentValue;
            bool shouldBeActive = EvaluateAlarmCondition(def, currentValue, setpoint);

            // Handle on-delay
            if (shouldBeActive && !alarmState.IsActive)
            {
                if (def.OnDelay > 0)
                {
                    if (!alarmState.DelayActive)
                    {
                        alarmState.ConditionMetTime = DateTime.UtcNow;
                        alarmState.DelayActive = true;
                    }
                    else if ((DateTime.UtcNow - alarmState.ConditionMetTime)?.TotalSeconds >= def.OnDelay)
                    {
                        // Delay elapsed, activate alarm
                        ActivateAlarm(alarmState, currentValue);
                    }
                    return alarmState.IsActive;
                }
                else
                {
                    ActivateAlarm(alarmState, currentValue);
                }
            }
            else if (!shouldBeActive && alarmState.IsActive)
            {
                alarmState.DelayActive = false;
                alarmState.ConditionMetTime = null;

                // Check hysteresis before clearing
                if (!EvaluateAlarmConditionWithHysteresis(def, currentValue, setpoint))
                {
                    DeactivateAlarm(alarmState, currentValue);
                }
            }
            else if (!shouldBeActive)
            {
                alarmState.DelayActive = false;
                alarmState.ConditionMetTime = null;
            }

            return alarmState.IsActive;
        }

        /// <summary>
        /// Directly activates an alarm (for discrete/off-normal alarms).
        /// </summary>
        public void RaiseAlarm(string alarmId, string message = "")
        {
            if (!_alarms.TryGetValue(alarmId, out var alarmState))
            {
                return;
            }

            if (!alarmState.IsActive)
            {
                alarmState.LastMessage = string.IsNullOrEmpty(message)
                    ? alarmState.Definition.FormatMessage(0)
                    : message;
                ActivateAlarm(alarmState, 0);
            }
        }

        /// <summary>
        /// Directly clears an alarm (for discrete/off-normal alarms).
        /// </summary>
        public void ClearAlarm(string alarmId)
        {
            if (!_alarms.TryGetValue(alarmId, out var alarmState))
            {
                return;
            }

            if (alarmState.IsActive)
            {
                DeactivateAlarm(alarmState, alarmState.LastValue);
            }
        }

        /// <summary>
        /// Gets the current state of an alarm.
        /// </summary>
        public AlarmState? GetAlarmState(string alarmId)
        {
            return _alarms.TryGetValue(alarmId, out var state) ? state : null;
        }

        /// <summary>
        /// Gets all active alarms.
        /// </summary>
        public IEnumerable<AlarmState> GetActiveAlarms()
        {
            return _alarms.Values.Where(a => a.IsActive);
        }

        /// <summary>
        /// Gets count of active unacknowledged alarms.
        /// </summary>
        public int GetUnacknowledgedCount()
        {
            return _alarms.Values.Count(a => a.IsActive && !a.IsAcknowledged);
        }

        private bool EvaluateAlarmCondition(AlarmDefinition def, double value, double? setpoint)
        {
            switch (def.AlarmType)
            {
                case LimitAlarmType.High:
                    return def.HighLimit.HasValue && value > def.HighLimit.Value;

                case LimitAlarmType.HighHigh:
                    return def.HighHighLimit.HasValue && value > def.HighHighLimit.Value;

                case LimitAlarmType.Low:
                    return def.LowLimit.HasValue && value < def.LowLimit.Value;

                case LimitAlarmType.LowLow:
                    return def.LowLowLimit.HasValue && value < def.LowLowLimit.Value;

                case LimitAlarmType.Deviation:
                    if (def.DeviationLimit.HasValue)
                    {
                        double sp = setpoint ?? def.Setpoint ?? 0;
                        double deviation = def.DeviationIsPercent
                            ? Math.Abs(value - sp) / sp * 100
                            : Math.Abs(value - sp);
                        return deviation > def.DeviationLimit.Value;
                    }
                    return false;

                case LimitAlarmType.OffNormal:
                    // For boolean/discrete values, non-zero = abnormal
                    return value != 0;

                default:
                    return false;
            }
        }

        private bool EvaluateAlarmConditionWithHysteresis(AlarmDefinition def, double value, double? setpoint)
        {
            double hyst = def.Hysteresis;

            switch (def.AlarmType)
            {
                case LimitAlarmType.High:
                    return def.HighLimit.HasValue && value > (def.HighLimit.Value - hyst);

                case LimitAlarmType.HighHigh:
                    return def.HighHighLimit.HasValue && value > (def.HighHighLimit.Value - hyst);

                case LimitAlarmType.Low:
                    return def.LowLimit.HasValue && value < (def.LowLimit.Value + hyst);

                case LimitAlarmType.LowLow:
                    return def.LowLowLimit.HasValue && value < (def.LowLowLimit.Value + hyst);

                case LimitAlarmType.Deviation:
                    if (def.DeviationLimit.HasValue)
                    {
                        double sp = setpoint ?? def.Setpoint ?? 0;
                        double deviation = def.DeviationIsPercent
                            ? Math.Abs(value - sp) / sp * 100
                            : Math.Abs(value - sp);
                        return deviation > (def.DeviationLimit.Value - hyst);
                    }
                    return false;

                default:
                    return EvaluateAlarmCondition(def, value, setpoint);
            }
        }

        private void ActivateAlarm(AlarmState alarmState, double triggerValue)
        {
            lock (_lock)
            {
                var def = alarmState.Definition;

                alarmState.IsActive = true;
                alarmState.IsAcknowledged = false;
                alarmState.IsConfirmed = !def.RequiresConfirmation;
                alarmState.ActiveTime = DateTime.UtcNow;
                alarmState.TriggerValue = triggerValue;

                // Determine which limit was exceeded
                double? limitValue = def.AlarmType switch
                {
                    LimitAlarmType.High => def.HighLimit,
                    LimitAlarmType.HighHigh => def.HighHighLimit,
                    LimitAlarmType.Low => def.LowLimit,
                    LimitAlarmType.LowLow => def.LowLowLimit,
                    LimitAlarmType.Deviation => def.DeviationLimit,
                    _ => null
                };

                alarmState.LastMessage = def.FormatMessage(triggerValue, limitValue);

                // Update OPC UA alarm node
                UpdateAlarmNodeState(alarmState, true);

                // Report event to subscribed clients
                ReportAlarmEvent(alarmState, true);

                Console.WriteLine($"[ALARM ACTIVE] {alarmState.LastMessage}");
            }
        }

        private void DeactivateAlarm(AlarmState alarmState, double clearValue)
        {
            lock (_lock)
            {
                alarmState.IsActive = false;
                alarmState.DelayActive = false;
                alarmState.ConditionMetTime = null;

                // Update OPC UA alarm node
                UpdateAlarmNodeState(alarmState, false);

                // Report event to subscribed clients
                ReportAlarmEvent(alarmState, false);

                Console.WriteLine($"[ALARM CLEARED] {alarmState.Definition.AlarmId}: {alarmState.Definition.Name} returned to normal");
            }
        }

        private void UpdateAlarmNodeState(AlarmState alarmState, bool active)
        {
            var alarm = alarmState.AlarmNode;
            if (alarm == null) return;

            try
            {
                // Update active state
                if (alarm.ActiveState != null)
                {
                    alarm.ActiveState.Value = new LocalizedText("en", active ? "Active" : "Inactive");
                    if (alarm.ActiveState.Id != null)
                        alarm.ActiveState.Id.Value = active;
                    if (alarm.ActiveState.TransitionTime != null)
                        alarm.ActiveState.TransitionTime.Value = DateTime.UtcNow;
                    if (alarm.ActiveState.EffectiveDisplayName != null)
                        alarm.ActiveState.EffectiveDisplayName.Value = new LocalizedText("en", active ? "Active" : "Inactive");
                }

                // Update acknowledged state
                if (alarm.AckedState != null)
                {
                    alarm.AckedState.Value = new LocalizedText("en", alarmState.IsAcknowledged ? "Acknowledged" : "Unacknowledged");
                    if (alarm.AckedState.Id != null)
                        alarm.AckedState.Id.Value = alarmState.IsAcknowledged;
                }

                // Update message
                if (alarm.Message != null)
                {
                    alarm.Message.Value = new LocalizedText("en", alarmState.LastMessage);
                }

                // Update severity
                if (alarm.Severity != null)
                {
                    alarm.Severity.Value = alarmState.Definition.Severity;
                }

                // Update time
                if (alarm.Time != null)
                {
                    alarm.Time.Value = DateTime.UtcNow;
                }

                // Update retain flag (keep in event queue while active or unacknowledged)
                if (alarm.Retain != null)
                {
                    alarm.Retain.Value = active || !alarmState.IsAcknowledged;
                }

                // Update limit state for ExclusiveLimitAlarm
                if (alarm.LimitState != null && alarm.LimitState.CurrentState != null)
                {
                    string limitStateName = alarmState.Definition.AlarmType switch
                    {
                        LimitAlarmType.HighHigh => active ? "HighHigh" : "None",
                        LimitAlarmType.High => active ? "High" : "None",
                        LimitAlarmType.Low => active ? "Low" : "None",
                        LimitAlarmType.LowLow => active ? "LowLow" : "None",
                        _ => "None"
                    };
                    alarm.LimitState.CurrentState.Value = new LocalizedText("en", limitStateName);
                }

                alarm.ClearChangeMasks(BatchPlantNodeManager.ServerContext, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update alarm node state: {ex.Message}");
            }
        }

        private void ReportAlarmEvent(AlarmState alarmState, bool activating)
        {
            var alarm = alarmState.AlarmNode;
            if (alarm == null) return;

            try
            {
                // Create unique event ID
                _eventIdCounter++;
                byte[] eventId = new byte[16];
                BitConverter.GetBytes(_eventIdCounter).CopyTo(eventId, 0);
                BitConverter.GetBytes(DateTime.UtcNow.Ticks).CopyTo(eventId, 4);

                if (alarm.EventId != null)
                {
                    alarm.EventId.Value = eventId;
                }

                // Set event type
                if (alarm.EventType != null)
                {
                    alarm.EventType.Value = ObjectTypeIds.ExclusiveLimitAlarmType;
                }

                // Report the event through the alarm node
                // This notifies all subscribed clients
                alarm.ReportEvent(BatchPlantNodeManager.ServerContext, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to report alarm event: {ex.Message}");
            }
        }

        private ServiceResult OnAcknowledgeAlarm(
            ISystemContext context,
            ConditionState condition,
            byte[] eventId,
            LocalizedText comment)
        {
            try
            {
                var alarmId = condition.SymbolicName;
                if (_alarms.TryGetValue(alarmId, out var alarmState))
                {
                    alarmState.IsAcknowledged = true;
                    alarmState.AcknowledgeTime = DateTime.UtcNow;

                    Console.WriteLine($"[ALARM ACK] {alarmId}: Acknowledged by operator");

                    // Cast to AcknowledgeableConditionState to access AckedState
                    if (condition is AcknowledgeableConditionState ackCondition && ackCondition.AckedState != null)
                    {
                        ackCondition.AckedState.Value = new LocalizedText("en", "Acknowledged");
                        if (ackCondition.AckedState.Id != null)
                            ackCondition.AckedState.Id.Value = true;
                        if (ackCondition.AckedState.TransitionTime != null)
                            ackCondition.AckedState.TransitionTime.Value = DateTime.UtcNow;
                    }

                    // Log comment if provided
                    if (comment != null && !string.IsNullOrEmpty(comment.Text))
                    {
                        Console.WriteLine($"           Comment: {comment.Text}");
                    }

                    // Update retain flag - clear if alarm is inactive and now acknowledged
                    if (condition.Retain != null && !alarmState.IsActive)
                    {
                        condition.Retain.Value = false;
                    }

                    condition.ClearChangeMasks(context, true);

                    // Report the state change as an event
                    if (alarmState.AlarmNode != null)
                    {
                        alarmState.AlarmNode.ReportEvent(context, null);
                    }
                }

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Acknowledge failed: {ex.Message}");
                return new ServiceResult(StatusCodes.BadUnexpectedError);
            }
        }

        private ServiceResult OnConfirmAlarm(
            ISystemContext context,
            ConditionState condition,
            byte[] eventId,
            LocalizedText comment)
        {
            try
            {
                var alarmId = condition.SymbolicName;
                if (_alarms.TryGetValue(alarmId, out var alarmState))
                {
                    if (!alarmState.IsAcknowledged)
                    {
                        return new ServiceResult(StatusCodes.BadInvalidState);
                    }

                    alarmState.IsConfirmed = true;
                    alarmState.ConfirmTime = DateTime.UtcNow;

                    Console.WriteLine($"[ALARM CONFIRM] {alarmId}: Confirmed by operator");

                    if (condition is AcknowledgeableConditionState ackCondition && ackCondition.ConfirmedState != null)
                    {
                        ackCondition.ConfirmedState.Value = new LocalizedText("en", "Confirmed");
                        if (ackCondition.ConfirmedState.Id != null)
                            ackCondition.ConfirmedState.Id.Value = true;
                        if (ackCondition.ConfirmedState.TransitionTime != null)
                            ackCondition.ConfirmedState.TransitionTime.Value = DateTime.UtcNow;
                    }

                    condition.ClearChangeMasks(context, true);

                    // Report the state change as an event
                    if (alarmState.AlarmNode != null)
                    {
                        alarmState.AlarmNode.ReportEvent(context, null);
                    }
                }

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Confirm failed: {ex.Message}");
                return new ServiceResult(StatusCodes.BadUnexpectedError);
            }
        }

        /// <summary>
        /// Gets a summary of alarm statistics.
        /// </summary>
        public (int Total, int Active, int Unacknowledged, int Unconfirmed) GetAlarmSummary()
        {
            var alarms = _alarms.Values.ToList();
            return (
                Total: alarms.Count,
                Active: alarms.Count(a => a.IsActive),
                Unacknowledged: alarms.Count(a => a.IsActive && !a.IsAcknowledged),
                Unconfirmed: alarms.Count(a => a.IsActive && a.IsAcknowledged && !a.IsConfirmed && a.Definition.RequiresConfirmation)
            );
        }

        /// <summary>
        /// Gets the Alarms folder node for event subscriptions.
        /// </summary>
        public FolderState? GetAlarmsFolder() => _alarmsFolder;
    }
}