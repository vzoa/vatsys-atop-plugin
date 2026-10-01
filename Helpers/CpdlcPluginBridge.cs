using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.Helpers;

/// <summary>
/// Reflection-based bridge to the CPDLCPlugin.
/// Discovers the plugin at runtime via vatSys's MEF plugin loader.
/// Falls back gracefully when the CPDLC plugin is not installed.
/// </summary>
public static class CpdlcPluginBridge
{
    public enum CpdlcConnectionState
    {
        Unknown,
        NotConnected,
        NextDataAuthority,
        CurrentDataAuthority
    }

    private static bool _initialized;
    private static bool _available;

    // Cached reflection targets — CPDLCPlugin.Plugin instance
    private static object? _cpdlcPlugin;

    // ConnectionManager (public property on Plugin)
    private static PropertyInfo? _connectionManagerProp;
    private static PropertyInfo? _connMgrIsConnectedProp;
    private static PropertyInfo? _connMgrStationIdentifierProp;

    // ServiceProvider (non-public property on Plugin)
    private static PropertyInfo? _serviceProviderProp;

    // AircraftConnectionStore type and All() method
    private static Type? _aircraftConnectionStoreType;
    private static MethodInfo? _storeAllMethod;

    // AircraftConnection properties
    private static PropertyInfo? _connCallsignProp;
    private static PropertyInfo? _connStationIdProp;
    private static PropertyInfo? _connDataAuthorityStateProp;

    // DataAuthorityState enum values
    private static object? _cdaValue;
    private static object? _ndaValue;

    // DialogueStore type and All() method
    private static Type? _dialogueStoreType;
    private static MethodInfo? _dialogueAllMethod;

    // DialogueDto properties
    private static PropertyInfo? _dialogueCallsignProp;
    private static PropertyInfo? _dialogueIsClosedProp;
    private static PropertyInfo? _dialogueIsArchivedProp;
    private static PropertyInfo? _dialogueMessagesProp;

    // Message type detection
    private static Type? _downlinkMessageType;
    private static PropertyInfo? _msgIsClosedProp;
    private static PropertyInfo? _msgIsAcknowledgedProp;
    private static PropertyInfo? _msgMessageIdProp;
    private static PropertyInfo? _msgMessageReferenceProp;

    // UplinkMessageDto properties (for full dialogue transcript)
    private static Type? _uplinkMessageType;
    private static PropertyInfo? _uplinkContentProp;
    private static PropertyInfo? _uplinkSentProp;

    // MediatR IMediator — resolved from ServiceProvider
    private static Type? _mediatorType;

    // OpenEditorWindowRequest type and constructor
    private static Type? _openEditorRequestType;

    // BeginDialogueRequest(Recipient, ResponseType, Content) — starts a new dialogue (no reply target)
    private static Type? _beginDialogueRequestType;

    // ReplyToDownlinkRequest(Recipient, DialogueId, DownlinkMessageId, ResponseType, Content) — replies within an existing dialogue
    private static Type? _replyToDownlinkRequestType;

    // SendStandbyUplinkRequest(DialogueId, DownlinkMessageId, Recipient) type and constructor
    private static Type? _sendStandbyRequestType;

    // SendUnableUplinkRequest(DialogueId, DownlinkMessageId, Recipient, Reason) type and constructor
    private static Type? _sendUnableRequestType;

    // ConnectRequest type and constructor
    private static Type? _connectRequestType;

    // DisconnectRequest type (parameterless)
    private static Type? _disconnectRequestType;

    // PluginConfiguration properties for connect
    private static PropertyInfo? _configServerEndpointProp;
    private static PropertyInfo? _configStationsProp;

    // UplinkMessagesConfiguration — from PluginConfiguration.UplinkMessages
    private static Type? _pluginConfigType;
    private static PropertyInfo? _uplinkMessagesProp;

    // UplinkMessagesConfiguration properties
    private static PropertyInfo? _masterMessagesProp;
    private static PropertyInfo? _permanentMessagesProp;
    private static PropertyInfo? _groupsProp;

    // MasterMessage properties
    private static PropertyInfo? _masterIdProp;
    private static PropertyInfo? _masterTemplateProp;
    private static PropertyInfo? _masterParametersProp;
    private static PropertyInfo? _masterResponseTypeProp;

    // UplinkMessageReference properties
    private static PropertyInfo? _refMessageIdProp;
    private static PropertyInfo? _refDefaultParamsProp;
    private static PropertyInfo? _refResponseTypeProp;

    // UplinkMessageGroup properties
    private static PropertyInfo? _groupNameProp;
    private static PropertyInfo? _groupMessagesProp;

    // UplinkMessageParameter properties
    private static PropertyInfo? _paramNameProp;
    private static PropertyInfo? _paramTypeProp;

    // DownlinkMessageDto properties
    private static PropertyInfo? _downlinkContentProp;
    private static PropertyInfo? _downlinkReceivedProp;
    private static PropertyInfo? _downlinkMessageIdProp;
    private static PropertyInfo? _downlinkResponseTypeProp;

    // CpdlcUplinkResponseType enum values
    private static Type? _uplinkResponseTypeEnum;

    // Dialogue ID
    private static PropertyInfo? _dialogueIdProp;

    // Cache to avoid repeated reflection per frame — case-insensitive because the CPDLC server's
    // reported callsign casing isn't guaranteed to match vatSys's FDR.Callsign casing exactly
    // (e.g. an aircraft's ACARS/CPDLC client may log on with different casing). Without this,
    // GetConnectionState/HasOpenDownlinks would silently miss a genuinely-cached connection right
    // after logon whenever the casing differs, making the CPDLC symbol never appear.
    private static readonly ConcurrentDictionary<string, CpdlcConnectionState> _connectionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> _downlinkCache = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _lastCacheRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(2);

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    /// <summary>
    /// Gets the CPDLC connection state for a callsign.
    /// Returns Unknown if the CPDLC plugin is not available.
    /// </summary>
    public static CpdlcConnectionState GetConnectionState(string callsign)
    {
        if (!IsAvailable || string.IsNullOrEmpty(callsign)) return CpdlcConnectionState.Unknown;

        RefreshCacheIfNeeded();
        return _connectionCache.TryGetValue(callsign, out var state) ? state : CpdlcConnectionState.NotConnected;
    }

    /// <summary>
    /// Returns true if there are open (unclosed/unacknowledged) downlink messages for a callsign.
    /// </summary>
    public static bool HasOpenDownlinks(string callsign)
    {
        if (!IsAvailable || string.IsNullOrEmpty(callsign)) return false;

        RefreshCacheIfNeeded();
        return _downlinkCache.TryGetValue(callsign, out var has) && has;
    }

    private static void RefreshCacheIfNeeded()
    {
        if (DateTime.UtcNow - _lastCacheRefresh < CacheLifetime) return;

        try
        {
            RefreshConnectionCache();
            RefreshDownlinkCache();
            _lastCacheRefresh = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge cache refresh: {ex.Message}", ex));
        }
    }

    private static void RefreshConnectionCache()
    {
        _connectionCache.Clear();

        var connManager = _connectionManagerProp?.GetValue(_cpdlcPlugin);
        if (connManager == null) return;

        // _connMgrIsConnectedProp may be null if SignalRConnectionManager type name didn't match —
        // fall back to a dynamic lookup on the runtime object so we don't silently skip the cache.
        var isConnectedProp = _connMgrIsConnectedProp
            ?? connManager.GetType().GetProperty("IsConnected", BindingFlags.Public | BindingFlags.Instance);
        var isConnected = (bool?)isConnectedProp?.GetValue(connManager) ?? false;
        if (!isConnected) return;

        // Same resilience for StationIdentifier.
        var stationIdPropInfo = _connMgrStationIdentifierProp
            ?? connManager.GetType().GetProperty("StationIdentifier", BindingFlags.Public | BindingFlags.Instance);
        var stationId = stationIdPropInfo?.GetValue(connManager) as string;
        if (string.IsNullOrEmpty(stationId)) return;

        var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
        if (sp == null) return;

        var store = sp.GetService(_aircraftConnectionStoreType!);
        if (store == null) return;

        // Call All(CancellationToken) — returns Task<IReadOnlyCollection<AircraftConnection>>
        var task = _storeAllMethod!.Invoke(store, new object[] { CancellationToken.None });
        // Synchronously get the result
        var awaiter = task!.GetType().GetMethod("GetAwaiter")!.Invoke(task, null);
        var connections = awaiter!.GetType().GetMethod("GetResult")!.Invoke(awaiter, null) as IEnumerable;
        if (connections == null) return;

        foreach (var conn in connections)
        {
            var callsign = _connCallsignProp?.GetValue(conn) as string;
            var daState = _connDataAuthorityStateProp?.GetValue(conn);

            if (string.IsNullOrEmpty(callsign)) continue;
            var key = callsign!; // non-null guaranteed by IsNullOrEmpty guard above

            // Only track connections for our station — but only apply the filter when we
            // successfully resolved the StationId property.  If _connStationIdProp is null
            // (property name mismatch in this CPDLCPlugin version) every connection would
            // read as null and be filtered out, leaving the cache permanently empty.
            if (_connStationIdProp != null)
            {
                var connStationId = _connStationIdProp.GetValue(conn) as string;
                if (!string.Equals(connStationId, stationId, StringComparison.OrdinalIgnoreCase)) continue;
            }

            // Prefer the pre-initialized enum values for a fast equality check.
            // Fall back to string-name matching when the DataAuthorityState type lookup
            // failed (e.g. ILRepack renamed the type), which would leave _cdaValue/_ndaValue
            // null and cause Equals(daState, null) to always be false — silently emptying
            // the cache for every aircraft.
            if (_cdaValue != null && _ndaValue != null)
            {
                if (Equals(daState, _cdaValue))
                    _connectionCache[key] = CpdlcConnectionState.CurrentDataAuthority;
                else if (Equals(daState, _ndaValue))
                    _connectionCache[key] = CpdlcConnectionState.NextDataAuthority;
            }
            else if (daState != null)
            {
                var stateName = daState.ToString() ?? "";
                if (stateName.IndexOf("CurrentDataAuthority", StringComparison.OrdinalIgnoreCase) >= 0)
                    _connectionCache[key] = CpdlcConnectionState.CurrentDataAuthority;
                else if (stateName.IndexOf("NextDataAuthority", StringComparison.OrdinalIgnoreCase) >= 0)
                    _connectionCache[key] = CpdlcConnectionState.NextDataAuthority;
            }
        }
    }

    private static void RefreshDownlinkCache()
    {
        _downlinkCache.Clear();

        if (_dialogueStoreType == null || _dialogueAllMethod == null) return;

        var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
        if (sp == null) return;

        var dialogueStore = sp.GetService(_dialogueStoreType);
        if (dialogueStore == null) return;

        // Call All(CancellationToken)
        var task = _dialogueAllMethod.Invoke(dialogueStore, new object[] { CancellationToken.None });
        var awaiter = task!.GetType().GetMethod("GetAwaiter")!.Invoke(task, null);
        var dialogues = awaiter!.GetType().GetMethod("GetResult")!.Invoke(awaiter, null) as Array;
        if (dialogues == null) return;

        foreach (var dialogue in dialogues)
        {
            var callsign = _dialogueCallsignProp?.GetValue(dialogue) as string;
            if (string.IsNullOrEmpty(callsign)) continue;

            var isClosed = (bool?)_dialogueIsClosedProp?.GetValue(dialogue) ?? true;
            var isArchived = (bool?)_dialogueIsArchivedProp?.GetValue(dialogue) ?? true;
            if (isClosed && isArchived) continue;

            var messages = _dialogueMessagesProp?.GetValue(dialogue) as IEnumerable;
            if (messages == null) continue;

            foreach (var msg in messages)
            {
                if (!_downlinkMessageType!.IsInstanceOfType(msg)) continue;

                var msgClosed = (bool?)_msgIsClosedProp?.GetValue(msg) ?? true;
                var msgAcked = (bool?)_msgIsAcknowledgedProp?.GetValue(msg) ?? true;

                if (!msgClosed || !msgAcked)
                {
                    _downlinkCache[callsign] = true;
                    break;
                }
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            Initialize();
        }
        catch (Exception ex)
        {
            _available = false;
            Errors.Add(new Exception($"CpdlcPluginBridge init: {ex.Message}", ex));
        }
    }

    private static void Initialize()
    {
        // 1. Find the CPDLCPlugin assembly
        Assembly? cpdlcAssembly = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "CPDLCPlugin")
            {
                cpdlcAssembly = asm;
                break;
            }
        }

        if (cpdlcAssembly == null)
        {
            _available = false;
            return;
        }

        // 2. Find the CPDLCPlugin.Plugin type
        var pluginType = cpdlcAssembly.GetType("CPDLCPlugin.Plugin");
        if (pluginType == null) { _available = false; return; }

        // 3. Find the plugin instance via vatSys's internal Plugins class
        var pluginsType = typeof(FDP2).Assembly.GetType("vatsys.Plugin.Plugins");
        if (pluginsType == null) { _available = false; return; }

        var loadedPluginsProp = pluginsType.GetProperty("LoadedPlugins", BindingFlags.Public | BindingFlags.Static);
        var loadedPlugins = loadedPluginsProp?.GetValue(null) as IList;
        if (loadedPlugins == null) { _available = false; return; }

        // The wrapper type has a private 'plugin' field containing the IPlugin instance
        foreach (var wrapper in loadedPlugins)
        {
            var wrapperType = wrapper.GetType();
            var nameProp = wrapperType.GetProperty("Name");
            var name = nameProp?.GetValue(wrapper) as string;

            if (name != null && name.StartsWith("CPDLC Plugin"))
            {
                var pluginField = wrapperType.GetField("plugin", BindingFlags.NonPublic | BindingFlags.Instance);
                _cpdlcPlugin = pluginField?.GetValue(wrapper);
                break;
            }
        }

        if (_cpdlcPlugin == null) { _available = false; return; }

        // 4. Cache reflection targets on the Plugin instance
        _connectionManagerProp = pluginType.GetProperty("ConnectionManager", BindingFlags.Public | BindingFlags.Instance);
        _serviceProviderProp = pluginType.GetProperty("ServiceProvider", BindingFlags.NonPublic | BindingFlags.Instance);

        // 5. ConnectionManager properties
        var connMgrType = cpdlcAssembly.GetType("CPDLCPlugin.Server.SignalRConnectionManager");
        if (connMgrType != null)
        {
            _connMgrIsConnectedProp = connMgrType.GetProperty("IsConnected", BindingFlags.Public | BindingFlags.Instance);
            _connMgrStationIdentifierProp = connMgrType.GetProperty("StationIdentifier", BindingFlags.Public | BindingFlags.Instance);
        }

        // 6. AircraftConnectionStore
        _aircraftConnectionStoreType = cpdlcAssembly.GetType("CPDLCPlugin.AircraftConnectionStore");
        if (_aircraftConnectionStoreType != null)
        {
            _storeAllMethod = _aircraftConnectionStoreType.GetMethod("All");
        }

        // 7. AircraftConnection properties
        var connType = cpdlcAssembly.GetType("CPDLCPlugin.AircraftConnection");
        if (connType != null)
        {
            _connCallsignProp = connType.GetProperty("Callsign");
            _connStationIdProp = connType.GetProperty("StationId");
            _connDataAuthorityStateProp = connType.GetProperty("DataAuthorityState");
        }

        // 8. DataAuthorityState enum — may be a separate assembly or ILRepacked into CPDLCPlugin
        Assembly? contractsAssembly = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "CPDLCServer.Contracts")
            {
                contractsAssembly = asm;
                break;
            }
        }
        // ILRepack merges CPDLCServer.Contracts into CPDLCPlugin.dll — fall back to that
        var contractsOrPlugin = contractsAssembly ?? cpdlcAssembly;

        {
            var daStateType = contractsOrPlugin.GetType("CPDLCServer.Contracts.DataAuthorityState");
            if (daStateType != null)
            {
                _cdaValue = Enum.Parse(daStateType, "CurrentDataAuthority");
                _ndaValue = Enum.Parse(daStateType, "NextDataAuthority");
            }
        }

        // 9. DialogueStore
        _dialogueStoreType = cpdlcAssembly.GetType("CPDLCPlugin.DialogueStore");
        if (_dialogueStoreType != null)
        {
            _dialogueAllMethod = _dialogueStoreType.GetMethod("All");
        }

        // 10. Dialogue DTO types — may be in separate contracts assembly or ILRepacked into CPDLCPlugin
        {
            var dialogueDtoType = contractsOrPlugin.GetType("CPDLCServer.Contracts.DialogueDto");
            if (dialogueDtoType != null)
            {
                _dialogueCallsignProp = dialogueDtoType.GetProperty("AircraftCallsign");
                _dialogueIsClosedProp = dialogueDtoType.GetProperty("IsClosed");
                _dialogueIsArchivedProp = dialogueDtoType.GetProperty("IsArchived");
                _dialogueMessagesProp = dialogueDtoType.GetProperty("Messages");
            }

            _downlinkMessageType = contractsOrPlugin.GetType("CPDLCServer.Contracts.DownlinkMessageDto");
            if (_downlinkMessageType != null)
            {
                _downlinkContentProp = _downlinkMessageType.GetProperty("Content");
                _downlinkReceivedProp = _downlinkMessageType.GetProperty("Received");
                _downlinkMessageIdProp = _downlinkMessageType.GetProperty("MessageId");
                _downlinkResponseTypeProp = _downlinkMessageType.GetProperty("ResponseType");
            }

            var baseMsgType = contractsOrPlugin.GetType("CPDLCServer.Contracts.CpdlcMessageDto");
            if (baseMsgType != null)
            {
                _msgIsClosedProp = baseMsgType.GetProperty("IsClosed");
                _msgIsAcknowledgedProp = baseMsgType.GetProperty("IsAcknowledged");
                _msgMessageIdProp = baseMsgType.GetProperty("MessageId");
                _msgMessageReferenceProp = baseMsgType.GetProperty("MessageReference");
            }

            _uplinkMessageType = contractsOrPlugin.GetType("CPDLCServer.Contracts.UplinkMessageDto");
            if (_uplinkMessageType != null)
            {
                _uplinkContentProp = _uplinkMessageType.GetProperty("Content");
                _uplinkSentProp = _uplinkMessageType.GetProperty("Sent");
            }

            _uplinkResponseTypeEnum = contractsOrPlugin.GetType("CPDLCServer.Contracts.CpdlcUplinkResponseType");
        }

        // 11. MediatR IMediator — CPDLCPlugin is deployed as a single ILRepack-merged assembly
        // (see the RepackPlugin build target, which merges MediatR + all other deps into
        // CPDLCPlugin.dll with internalize:false). The canonical IMediator type used by its
        // DI container therefore lives directly inside cpdlcAssembly. Check there FIRST —
        // scanning other loaded assemblies first risked binding to an unrelated same-named
        // MediatR.IMediator type (different Type identity), which would silently make
        // sp.GetService(_mediatorType) return null and drop every uplink send.
        _mediatorType = cpdlcAssembly.GetType("MediatR.IMediator");
        if (_mediatorType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "MediatR.Contracts" || asm.GetName().Name == "MediatR")
                {
                    _mediatorType = asm.GetType("MediatR.IMediator");
                    if (_mediatorType != null) break;
                }
            }
        }

        // 12. MediatR request types in CPDLCPlugin.Messages namespace
        _beginDialogueRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.BeginDialogueRequest");
        _replyToDownlinkRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.ReplyToDownlinkRequest");
        _sendStandbyRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.SendStandbyUplinkRequest");
        _sendUnableRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.SendUnableUplinkRequest");
        _openEditorRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.OpenEditorWindowRequest");
        _connectRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.ConnectRequest");
        _disconnectRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.DisconnectRequest");

        // 13. PluginConfiguration — for reading message templates
        _pluginConfigType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.PluginConfiguration");
        if (_pluginConfigType != null)
        {
            _uplinkMessagesProp = _pluginConfigType.GetProperty("UplinkMessages");
            _configServerEndpointProp = _pluginConfigType.GetProperty("ServerEndpoint");
            _configStationsProp = _pluginConfigType.GetProperty("Stations");
        }

        // 14. UplinkMessagesConfiguration properties
        var uplinkMsgConfigType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.UplinkMessagesConfiguration");
        if (uplinkMsgConfigType != null)
        {
            _masterMessagesProp = uplinkMsgConfigType.GetProperty("MasterMessages");
            _permanentMessagesProp = uplinkMsgConfigType.GetProperty("PermanentMessages");
            _groupsProp = uplinkMsgConfigType.GetProperty("Groups");
        }

        // 15. UplinkMessageTemplate properties
        var templateType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.UplinkMessageTemplate");
        if (templateType != null)
        {
            _masterIdProp = templateType.GetProperty("Id");
            _masterTemplateProp = templateType.GetProperty("Template");
            _masterParametersProp = templateType.GetProperty("Parameters");
            _masterResponseTypeProp = templateType.GetProperty("ResponseType");
        }

        // 16. UplinkMessageParameter properties
        var paramType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.UplinkMessageParameter");
        if (paramType != null)
        {
            _paramNameProp = paramType.GetProperty("Name");
            _paramTypeProp = paramType.GetProperty("Type");
        }

        // 17. UplinkMessageReference properties
        var refType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.UplinkMessageReference");
        if (refType != null)
        {
            _refMessageIdProp = refType.GetProperty("MessageId");
            _refDefaultParamsProp = refType.GetProperty("DefaultParameters");
            _refResponseTypeProp = refType.GetProperty("ResponseType");
        }

        // 18. UplinkMessageGroup properties
        var groupType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.UplinkMessageGroup");
        if (groupType != null)
        {
            _groupNameProp = groupType.GetProperty("Name");
            _groupMessagesProp = groupType.GetProperty("Messages");
        }

        // 19. DialogueDto.Id
        {
            var dialogueDtoType = contractsOrPlugin.GetType("CPDLCServer.Contracts.DialogueDto");
            if (dialogueDtoType != null)
            {
                _dialogueIdProp = dialogueDtoType.GetProperty("Id");
            }
        }

        _available = _connectionManagerProp != null
                     && _serviceProviderProp != null
                     && _aircraftConnectionStoreType != null
                     && _storeAllMethod != null;
    }

    /// <summary>
    /// Returns a diagnostic string describing bridge initialization state and live connection cache.
    /// </summary>
    public static string GetDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== CpdlcPluginBridge Diagnostics ===");
        sb.AppendLine($"Initialized: {_initialized}");
        sb.AppendLine($"Available:   {_available}");
        sb.AppendLine($"Plugin instance found: {_cpdlcPlugin != null}");
        sb.AppendLine($"ConnectionManagerProp: {_connectionManagerProp != null}");
        sb.AppendLine($"ServiceProviderProp:   {_serviceProviderProp != null}");
        sb.AppendLine($"AircraftConnectionStoreType: {_aircraftConnectionStoreType != null}");
        sb.AppendLine($"StoreAllMethod: {_storeAllMethod != null}");
        sb.AppendLine($"CDA enum value: {_cdaValue}");
        sb.AppendLine($"NDA enum value: {_ndaValue}");
        sb.AppendLine($"MediatorType found: {_mediatorType != null} ({_mediatorType?.FullName})");
        sb.AppendLine($"BeginDialogueRequestType found: {_beginDialogueRequestType != null}");
        sb.AppendLine($"ReplyToDownlinkRequestType found: {_replyToDownlinkRequestType != null}");
        sb.AppendLine($"UplinkResponseTypeEnum found: {_uplinkResponseTypeEnum != null}");

        if (_available && _cpdlcPlugin != null)
        {
            try
            {
                var connManager = _connectionManagerProp?.GetValue(_cpdlcPlugin);
                var isConnected = (bool?)_connMgrIsConnectedProp?.GetValue(connManager) ?? false;
                var stationId = _connMgrStationIdentifierProp?.GetValue(connManager) as string;
                sb.AppendLine($"ConnectionManager.IsConnected: {isConnected}");
                sb.AppendLine($"ConnectionManager.StationIdentifier: '{stationId}'");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"ConnectionManager read error: {ex.Message}");
            }

            // Force a fresh cache refresh
            _lastCacheRefresh = DateTime.MinValue;
            try { RefreshConnectionCache(); } catch (Exception ex) { sb.AppendLine($"Cache refresh error: {ex.Message}"); }

            sb.AppendLine($"Connection cache ({_connectionCache.Count} entries):");
            foreach (var kvp in _connectionCache)
                sb.AppendLine($"  {kvp.Key} => {kvp.Value}");

            if (_connectionCache.Count == 0)
                sb.AppendLine("  (empty — no aircraft logged on to our station)");
        }

        return sb.ToString();
    }

    // =========================================================================
    // Editor / Clearance Window support methods
    // =========================================================================

    /// <summary>
    /// Reads the uplink message configuration from CPDLCPlugin.
    /// Returns null if the bridge is unavailable or configuration cannot be read.
    /// </summary>
    public static AtopUplinkMessagesConfig? GetUplinkMessagesConfig()
    {
        if (!IsAvailable || _pluginConfigType == null || _uplinkMessagesProp == null) return null;

        try
        {
            var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
            if (sp == null) return null;

            var config = sp.GetService(_pluginConfigType);
            if (config == null) return null;

            var uplinkMessages = _uplinkMessagesProp.GetValue(config);
            if (uplinkMessages == null) return null;

            var result = new AtopUplinkMessagesConfig();

            // Read MasterMessages[]
            if (_masterMessagesProp?.GetValue(uplinkMessages) is Array masterArray)
            {
                var masters = new List<AtopUplinkTemplate>();
                foreach (var m in masterArray)
                {
                    var template = new AtopUplinkTemplate
                    {
                        Id = (int?)_masterIdProp?.GetValue(m) ?? 0,
                        Template = _masterTemplateProp?.GetValue(m) as string ?? "",
                        ResponseType = Convert.ToInt32(_masterResponseTypeProp?.GetValue(m) ?? 0)
                    };

                    if (_masterParametersProp?.GetValue(m) is Array paramArray)
                    {
                        var parms = new List<AtopUplinkParameter>();
                        foreach (var p in paramArray)
                        {
                            parms.Add(new AtopUplinkParameter
                            {
                                Name = _paramNameProp?.GetValue(p) as string ?? "",
                                Type = _paramTypeProp?.GetValue(p)?.ToString() ?? ""
                            });
                        }
                        template.Parameters = parms.ToArray();
                    }

                    masters.Add(template);
                }
                result.MasterMessages = masters.ToArray();
            }

            // Read PermanentMessages[]
            if (_permanentMessagesProp?.GetValue(uplinkMessages) is Array permArray)
            {
                result.PermanentMessages = ReadMessageReferences(permArray);
            }

            // Read Groups[]
            if (_groupsProp?.GetValue(uplinkMessages) is Array groupArray)
            {
                var groups = new List<AtopMessageGroup>();
                foreach (var g in groupArray)
                {
                    var group = new AtopMessageGroup
                    {
                        Name = _groupNameProp?.GetValue(g) as string ?? ""
                    };

                    if (_groupMessagesProp?.GetValue(g) is Array groupMsgs)
                    {
                        group.Messages = ReadMessageReferences(groupMsgs);
                    }

                    groups.Add(group);
                }
                result.Groups = groups.ToArray();
            }

            return result;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.GetUplinkMessagesConfig: {ex.Message}", ex));
            return null;
        }
    }

    private static AtopMessageReference[] ReadMessageReferences(Array array)
    {
        var refs = new List<AtopMessageReference>();
        foreach (var r in array)
        {
            var msgRef = new AtopMessageReference
            {
                MessageId = (int?)_refMessageIdProp?.GetValue(r) ?? 0,
                ResponseType = _refResponseTypeProp?.GetValue(r) is object rt ? (int?)Convert.ToInt32(rt) : null
            };

            if (_refDefaultParamsProp?.GetValue(r) is IDictionary dict)
            {
                msgRef.DefaultParameters = new Dictionary<string, string>();
                foreach (DictionaryEntry entry in dict)
                {
                    msgRef.DefaultParameters[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
                }
            }

            refs.Add(msgRef);
        }
        return refs.ToArray();
    }

    /// <summary>
    /// Gets detailed downlink message info for a callsign (for display in Clearance window).
    /// Returns empty list if unavailable.
    /// </summary>
    public static List<AtopDownlinkInfo> GetOpenDownlinkDetails(string callsign)
    {
        var results = new List<AtopDownlinkInfo>();
        if (!IsAvailable || string.IsNullOrEmpty(callsign)) return results;

        try
        {
            var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
            if (sp == null || _dialogueStoreType == null || _dialogueAllMethod == null) return results;

            var dialogueStore = sp.GetService(_dialogueStoreType);
            if (dialogueStore == null) return results;

            var task = _dialogueAllMethod.Invoke(dialogueStore, new object[] { CancellationToken.None });
            var awaiter = task!.GetType().GetMethod("GetAwaiter")!.Invoke(task, null);
            var dialogues = awaiter!.GetType().GetMethod("GetResult")!.Invoke(awaiter, null) as Array;
            if (dialogues == null) return results;

            foreach (var dialogue in dialogues)
            {
                var dlgCallsign = _dialogueCallsignProp?.GetValue(dialogue) as string;
                if (!string.Equals(dlgCallsign, callsign, StringComparison.OrdinalIgnoreCase)) continue;

                var isClosed = (bool?)_dialogueIsClosedProp?.GetValue(dialogue) ?? true;
                var isArchived = (bool?)_dialogueIsArchivedProp?.GetValue(dialogue) ?? true;
                if (isClosed && isArchived) continue;

                var dialogueId = _dialogueIdProp?.GetValue(dialogue) is Guid dgId ? dgId : Guid.Empty;

                var messages = _dialogueMessagesProp?.GetValue(dialogue) as IEnumerable;
                if (messages == null) continue;

                foreach (var msg in messages)
                {
                    if (!_downlinkMessageType!.IsInstanceOfType(msg)) continue;

                    var msgClosed = (bool?)_msgIsClosedProp?.GetValue(msg) ?? true;
                    var msgAcked = (bool?)_msgIsAcknowledgedProp?.GetValue(msg) ?? true;
                    if (msgClosed && msgAcked) continue;

                    results.Add(new AtopDownlinkInfo
                    {
                        DialogueId = dialogueId,
                        MessageId = (int?)_downlinkMessageIdProp?.GetValue(msg) ?? 0,
                        Content = _downlinkContentProp?.GetValue(msg) as string ?? "",
                        Received = _downlinkReceivedProp?.GetValue(msg) is DateTimeOffset dto ? dto : DateTimeOffset.MinValue,
                        ResponseType = _downlinkResponseTypeProp?.GetValue(msg) is object rt ? (int?)Convert.ToInt32(rt) : null,
                        IsClosed = msgClosed,
                        IsAcknowledged = msgAcked
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.GetOpenDownlinkDetails: {ex.Message}", ex));
        }

        return results;
    }

    /// <summary>
    /// Gets detailed downlink message info across ALL aircraft (not scoped to a single callsign) —
    /// used to populate the Sector Queue window with pending CPDLC downlinks needing attention.
    /// Returns empty list if unavailable.
    /// </summary>
    public static List<AtopDownlinkInfo> GetAllOpenDownlinks()
    {
        var results = new List<AtopDownlinkInfo>();
        if (!IsAvailable) return results;

        try
        {
            var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
            if (sp == null || _dialogueStoreType == null || _dialogueAllMethod == null) return results;

            var dialogueStore = sp.GetService(_dialogueStoreType);
            if (dialogueStore == null) return results;

            var task = _dialogueAllMethod.Invoke(dialogueStore, new object[] { CancellationToken.None });
            var awaiter = task!.GetType().GetMethod("GetAwaiter")!.Invoke(task, null);
            var dialogues = awaiter!.GetType().GetMethod("GetResult")!.Invoke(awaiter, null) as Array;
            if (dialogues == null) return results;

            foreach (var dialogue in dialogues)
            {
                var isClosed = (bool?)_dialogueIsClosedProp?.GetValue(dialogue) ?? true;
                var isArchived = (bool?)_dialogueIsArchivedProp?.GetValue(dialogue) ?? true;
                if (isClosed && isArchived) continue;

                var dlgCallsign = _dialogueCallsignProp?.GetValue(dialogue) as string ?? "";
                var dialogueId = _dialogueIdProp?.GetValue(dialogue) is Guid dgId ? dgId : Guid.Empty;

                var messages = _dialogueMessagesProp?.GetValue(dialogue) as IEnumerable;
                if (messages == null) continue;

                foreach (var msg in messages)
                {
                    if (_downlinkMessageType == null || !_downlinkMessageType.IsInstanceOfType(msg)) continue;

                    var msgClosed = (bool?)_msgIsClosedProp?.GetValue(msg) ?? true;
                    var msgAcked = (bool?)_msgIsAcknowledgedProp?.GetValue(msg) ?? true;
                    if (msgClosed && msgAcked) continue;

                    results.Add(new AtopDownlinkInfo
                    {
                        Callsign = dlgCallsign,
                        DialogueId = dialogueId,
                        MessageId = (int?)_downlinkMessageIdProp?.GetValue(msg) ?? 0,
                        Content = _downlinkContentProp?.GetValue(msg) as string ?? "",
                        Received = _downlinkReceivedProp?.GetValue(msg) is DateTimeOffset dto ? dto : DateTimeOffset.MinValue,
                        ResponseType = _downlinkResponseTypeProp?.GetValue(msg) is object rt ? (int?)Convert.ToInt32(rt) : null,
                        IsClosed = msgClosed,
                        IsAcknowledged = msgAcked
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.GetAllOpenDownlinks: {ex.Message}", ex));
        }

        return results;
    }

    /// <summary>
    /// Returns the full message transcript (uplinks + downlinks, chronologically ordered) for the
    /// aircraft's current (non-archived) CPDLC dialogue, as tracked by CPDLCPlugin's DialogueStore.
    /// This reflects the actual, live CPDLC conversation rather than a locally re-derived subset,
    /// so the ATOP Clearance window can show/verify what was really sent and received.
    /// Returns an empty list if unavailable or no dialogue exists for the callsign.
    /// </summary>
    public static List<AtopDialogueMessage> GetDialogueMessages(string callsign)
    {
        var results = new List<AtopDialogueMessage>();
        if (!IsAvailable || string.IsNullOrEmpty(callsign)) return results;

        try
        {
            var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
            if (sp == null || _dialogueStoreType == null || _dialogueAllMethod == null) return results;

            var dialogueStore = sp.GetService(_dialogueStoreType);
            if (dialogueStore == null) return results;

            var task = _dialogueAllMethod.Invoke(dialogueStore, new object[] { CancellationToken.None });
            var awaiter = task!.GetType().GetMethod("GetAwaiter")!.Invoke(task, null);
            var dialogues = awaiter!.GetType().GetMethod("GetResult")!.Invoke(awaiter, null) as Array;
            if (dialogues == null) return results;

            // A DialogueStore only ever holds one active (non-archived) dialogue per aircraft —
            // that's the "current" conversation shown by the CPDLC plugin's own dialogue window.
            object? currentDialogue = null;
            foreach (var dialogue in dialogues)
            {
                var dlgCallsign = _dialogueCallsignProp?.GetValue(dialogue) as string;
                if (!string.Equals(dlgCallsign, callsign, StringComparison.OrdinalIgnoreCase)) continue;

                var isArchived = (bool?)_dialogueIsArchivedProp?.GetValue(dialogue) ?? true;
                if (isArchived) continue;

                currentDialogue = dialogue;
                break;
            }

            if (currentDialogue == null) return results;

            var currentDialogueId = _dialogueIdProp?.GetValue(currentDialogue) is Guid cdgId ? cdgId : Guid.Empty;

            var messages = _dialogueMessagesProp?.GetValue(currentDialogue) as IEnumerable;
            if (messages == null) return results;

            foreach (var msg in messages)
            {
                var isUplink = _uplinkMessageType?.IsInstanceOfType(msg) == true;
                var isDownlink = !isUplink && _downlinkMessageType?.IsInstanceOfType(msg) == true;
                if (!isUplink && !isDownlink) continue;

                var content = isUplink
                    ? _uplinkContentProp?.GetValue(msg) as string ?? ""
                    : _downlinkContentProp?.GetValue(msg) as string ?? "";

                var time = isUplink
                    ? (_uplinkSentProp?.GetValue(msg) is DateTimeOffset sent ? sent : DateTimeOffset.MinValue)
                    : (_downlinkReceivedProp?.GetValue(msg) is DateTimeOffset received ? received : DateTimeOffset.MinValue);

                results.Add(new AtopDialogueMessage
                {
                    DialogueId = currentDialogueId,
                    Direction = isUplink ? AtopDialogueDirection.Uplink : AtopDialogueDirection.Downlink,
                    MessageId = (int?)_msgMessageIdProp?.GetValue(msg) ?? 0,
                    MessageReference = _msgMessageReferenceProp?.GetValue(msg) as int?,
                    Content = content,
                    Time = time,
                    IsClosed = (bool?)_msgIsClosedProp?.GetValue(msg) ?? false,
                    IsAcknowledged = (bool?)_msgIsAcknowledgedProp?.GetValue(msg) ?? false
                });
            }

            results.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.GetDialogueMessages: {ex.Message}", ex));
        }

        return results;
    }

    /// <summary>
    /// Returns true if the CPDLC plugin has a WILCO downlink response for a callsign
    /// received at or after the given UTC timestamp.
    /// </summary>
    public static bool HasWilcoReadbackSince(string callsign, DateTimeOffset sinceUtc)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(callsign)) return false;

        try
        {
            var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
            if (sp == null || _dialogueStoreType == null || _dialogueAllMethod == null || _downlinkMessageType == null)
                return false;

            var dialogueStore = sp.GetService(_dialogueStoreType);
            if (dialogueStore == null) return false;

            var task = _dialogueAllMethod.Invoke(dialogueStore, new object[] { CancellationToken.None });
            var awaiter = task!.GetType().GetMethod("GetAwaiter")!.Invoke(task, null);
            var dialogues = awaiter!.GetType().GetMethod("GetResult")!.Invoke(awaiter, null) as Array;
            if (dialogues == null) return false;

            foreach (var dialogue in dialogues)
            {
                var dlgCallsign = _dialogueCallsignProp?.GetValue(dialogue) as string;
                if (!string.Equals(dlgCallsign, callsign, StringComparison.OrdinalIgnoreCase))
                    continue;

                var messages = _dialogueMessagesProp?.GetValue(dialogue) as IEnumerable;
                if (messages == null) continue;

                foreach (var msg in messages)
                {
                    if (!_downlinkMessageType.IsInstanceOfType(msg))
                        continue;

                    var received = _downlinkReceivedProp?.GetValue(msg) is DateTimeOffset dto
                        ? dto
                        : DateTimeOffset.MinValue;
                    if (received < sinceUtc)
                        continue;

                    var response = _downlinkResponseTypeProp?.GetValue(msg);
                    if (response == null)
                        continue;

                    // WILCO is part of the downlink's free-text Content, not its ResponseType
                    // enum (which is only ever NoResponse/ResponseRequired).
                    var content = _downlinkContentProp?.GetValue(msg) as string;
                    if (!string.IsNullOrEmpty(content)
                        && content.IndexOf("WILCO", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.HasWilcoReadbackSince: {ex.Message}", ex));
        }

        return false;
    }

    /// <summary>
    /// Sends an uplink CPDLC message via CPDLCPlugin's MediatR pipeline.
    /// responseType: 0=NoResponse, 1=WilcoUnable, 2=AffirmativeNegative, 3=Roger
    /// When <paramref name="dialogueId"/> and <paramref name="replyToDownlinkId"/> are both
    /// provided, this replies within the existing dialogue (ReplyToDownlinkRequest). Otherwise
    /// it starts a new dialogue (BeginDialogueRequest) — the server's SendUplink hub method was
    /// split into these two distinct operations and no longer exists as a single call.
    /// </summary>
    public static void SendUplink(string callsign, Guid? dialogueId, int? replyToDownlinkId, int responseType, string content)
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _uplinkResponseTypeEnum == null) return;

            var responseTypeValue = Enum.ToObject(_uplinkResponseTypeEnum, responseType);

            object? request;
            if (dialogueId.HasValue && replyToDownlinkId.HasValue)
            {
                if (_replyToDownlinkRequestType == null) return;

                // ReplyToDownlinkRequest(string Recipient, Guid DialogueId, int DownlinkMessageId, CpdlcUplinkResponseType ResponseType, string Content)
                request = Activator.CreateInstance(
                    _replyToDownlinkRequestType, callsign, dialogueId.Value, replyToDownlinkId.Value, responseTypeValue, content);
            }
            else
            {
                if (_beginDialogueRequestType == null) return;

                // BeginDialogueRequest(string Recipient, CpdlcUplinkResponseType ResponseType, string Content)
                request = Activator.CreateInstance(_beginDialogueRequestType, callsign, responseTypeValue, content);
            }

            if (request == null) return;

            InvokeMediatorSend(mediator, request);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.SendUplink: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Sends a STANDBY response to a downlink message.
    /// </summary>
    public static void SendStandby(Guid dialogueId, int downlinkMessageId, string callsign)
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _sendStandbyRequestType == null) return;

            // Construct SendStandbyUplinkRequest(Guid DialogueId, int DownlinkMessageId, string Recipient)
            var request = Activator.CreateInstance(_sendStandbyRequestType, dialogueId, downlinkMessageId, callsign);
            if (request == null) return;

            InvokeMediatorSend(mediator, request);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.SendStandby: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Sends an UNABLE response to a downlink message.
    /// </summary>
    public static void SendUnable(Guid dialogueId, int downlinkMessageId, string callsign, string reason = "")
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _sendUnableRequestType == null) return;

            // Construct SendUnableUplinkRequest(Guid DialogueId, int DownlinkMessageId, string Recipient, string Reason)
            var request = Activator.CreateInstance(_sendUnableRequestType, dialogueId, downlinkMessageId, callsign, reason);
            if (request == null) return;

            InvokeMediatorSend(mediator, request);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.SendUnable: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Opens the CPDLCPlugin's own editor window for a callsign (fallback).
    /// </summary>
    public static void OpenEditor(string callsign)
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _openEditorRequestType == null) return;

            // Construct OpenEditorWindowRequest(string Callsign)
            var request = Activator.CreateInstance(_openEditorRequestType, callsign);
            if (request == null) return;

            InvokeMediatorSend(mediator, request);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.OpenEditor: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Triggers a CPDLC server connect using the first station from the plugin configuration.
    /// Safe to call when the CPDLC plugin is not installed — no-ops silently.
    /// </summary>
    public static void TriggerConnect()
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _connectRequestType == null || _pluginConfigType == null) return;

            var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
            if (sp == null) return;

            var config = sp.GetService(_pluginConfigType);
            if (config == null) return;

            var serverEndpoint = _configServerEndpointProp?.GetValue(config) as string;
            var stations = _configStationsProp?.GetValue(config) as string[];
            if (string.IsNullOrEmpty(serverEndpoint) || stations == null || stations.Length == 0) return;

            var request = Activator.CreateInstance(_connectRequestType, serverEndpoint, stations[0]);
            if (request == null) return;

            InvokeMediatorSend(mediator, request);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.TriggerConnect: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Triggers a CPDLC server disconnect. Safe to call when the CPDLC plugin is not installed,
    /// or when there is no active connection — no-ops silently in both cases.
    /// </summary>
    public static void TriggerDisconnect()
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _disconnectRequestType == null) return;

            var request = Activator.CreateInstance(_disconnectRequestType);
            if (request == null) return;

            InvokeMediatorSend(mediator, request);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.TriggerDisconnect: {ex.Message}", ex));
        }
    }

    private static object? ResolveMediator()
    {
        if (_mediatorType == null) return null;

        var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
        return sp?.GetService(_mediatorType);
    }

    private static void InvokeMediatorSend(object mediator, object request)
    {
        var requestType = request.GetType();

        // MediatR's IMediator (via ISender) exposes two Send overloads:
        //   Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)  — generic
        //   Task<object?>   Send(object request, CancellationToken ct = default)                           — non-generic
        // Both are declared on ISender, not IMediator itself (IMediator is just an empty marker
        // interface: "interface IMediator : ISender, IPublisher { }"). Reflecting on _mediatorType
        // (IMediator) directly returns ZERO methods, since Type.GetMethods() on an interface only
        // returns members declared on that interface, not inherited ones. Reflect on the concrete
        // runtime object's type instead, which reliably exposes all implemented interface methods
        // as public instance methods regardless of interface layering.
        var mediatorType = mediator.GetType();

        var sendMethod = mediatorType.GetMethods()
            .FirstOrDefault(m => m.Name == "Send"
                                  && !m.IsGenericMethodDefinition
                                  && m.GetParameters() is { Length: 2 } nonGenericParams
                                  && nonGenericParams[0].ParameterType == typeof(object)
                                  && nonGenericParams[1].ParameterType == typeof(CancellationToken));

        if (sendMethod == null)
        {
            var genericSend = mediatorType.GetMethods()
                .FirstOrDefault(m => m.Name == "Send"
                                      && m.IsGenericMethodDefinition
                                      && m.GetParameters() is { Length: 2 } genericParams
                                      && genericParams[1].ParameterType == typeof(CancellationToken));

            var requestInterface = requestType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.Name.StartsWith("IRequest"));

            if (genericSend != null && requestInterface != null)
                sendMethod = genericSend.MakeGenericMethod(requestInterface.GetGenericArguments()[0]);
        }

        if (sendMethod == null)
        {
            Errors.Add(new Exception($"CpdlcPluginBridge.InvokeMediatorSend: could not resolve a usable IMediator.Send method for request type {requestType.FullName}"));
            return;
        }

        var task = sendMethod.Invoke(mediator, new[] { request, CancellationToken.None });
        // Fire-and-forget but observe exceptions
        if (task is Task t)
        {
            t.ContinueWith(faulted =>
            {
                if (faulted.Exception != null)
                    Errors.Add(new Exception($"CpdlcPluginBridge MediatR Send failed: {faulted.Exception.InnerException?.Message}", faulted.Exception.InnerException));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
