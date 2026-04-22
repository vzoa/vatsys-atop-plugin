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

    // MediatR IMediator — resolved from ServiceProvider
    private static Type? _mediatorType;

    // OpenEditorWindowRequest type and constructor
    private static Type? _openEditorRequestType;

    // SendUplinkRequest type and constructor
    private static Type? _sendUplinkRequestType;

    // SendStandbyUplinkRequest type and constructor
    private static Type? _sendStandbyRequestType;

    // SendUnableUplinkRequest type and constructor
    private static Type? _sendUnableRequestType;

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

    // Cache to avoid repeated reflection per frame
    private static readonly ConcurrentDictionary<string, CpdlcConnectionState> _connectionCache = new();
    private static readonly ConcurrentDictionary<string, bool> _downlinkCache = new();
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

        var isConnected = (bool?)_connMgrIsConnectedProp?.GetValue(connManager) ?? false;
        if (!isConnected) return;

        var stationId = _connMgrStationIdentifierProp?.GetValue(connManager) as string;
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
            var connStationId = _connStationIdProp?.GetValue(conn) as string;
            var daState = _connDataAuthorityStateProp?.GetValue(conn);

            if (string.IsNullOrEmpty(callsign)) continue;

            // Only track connections for our station
            if (!string.Equals(connStationId, stationId, StringComparison.OrdinalIgnoreCase)) continue;

            if (Equals(daState, _cdaValue))
                _connectionCache[callsign] = CpdlcConnectionState.CurrentDataAuthority;
            else if (Equals(daState, _ndaValue))
                _connectionCache[callsign] = CpdlcConnectionState.NextDataAuthority;
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

        // 8. DataAuthorityState enum — in CPDLCServer.Contracts assembly
        Assembly? contractsAssembly = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "CPDLCServer.Contracts")
            {
                contractsAssembly = asm;
                break;
            }
        }

        if (contractsAssembly != null)
        {
            var daStateType = contractsAssembly.GetType("CPDLCServer.Contracts.DataAuthorityState");
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

        // 10. Dialogue DTO types — in contracts assembly
        if (contractsAssembly != null)
        {
            var dialogueDtoType = contractsAssembly.GetType("CPDLCServer.Contracts.DialogueDto");
            if (dialogueDtoType != null)
            {
                _dialogueCallsignProp = dialogueDtoType.GetProperty("AircraftCallsign");
                _dialogueIsClosedProp = dialogueDtoType.GetProperty("IsClosed");
                _dialogueIsArchivedProp = dialogueDtoType.GetProperty("IsArchived");
                _dialogueMessagesProp = dialogueDtoType.GetProperty("Messages");
            }

            _downlinkMessageType = contractsAssembly.GetType("CPDLCServer.Contracts.DownlinkMessageDto");
            if (_downlinkMessageType != null)
            {
                _downlinkContentProp = _downlinkMessageType.GetProperty("Content");
                _downlinkReceivedProp = _downlinkMessageType.GetProperty("Received");
                _downlinkMessageIdProp = _downlinkMessageType.GetProperty("MessageId");
                _downlinkResponseTypeProp = _downlinkMessageType.GetProperty("ResponseType");
            }

            var baseMsgType = contractsAssembly.GetType("CPDLCServer.Contracts.CpdlcMessageDto");
            if (baseMsgType != null)
            {
                _msgIsClosedProp = baseMsgType.GetProperty("IsClosed");
                _msgIsAcknowledgedProp = baseMsgType.GetProperty("IsAcknowledged");
            }

            _uplinkResponseTypeEnum = contractsAssembly.GetType("CPDLCServer.Contracts.CpdlcUplinkResponseType");
        }

        // 11. MediatR IMediator — find through loaded assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "MediatR.Contracts" || asm.GetName().Name == "MediatR")
            {
                _mediatorType = asm.GetType("MediatR.IMediator");
                if (_mediatorType != null) break;
            }
        }

        // 12. MediatR request types in CPDLCPlugin.Messages namespace
        _sendUplinkRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.SendUplinkRequest");
        _sendStandbyRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.SendStandbyUplinkRequest");
        _sendUnableRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.SendUnableUplinkRequest");
        _openEditorRequestType = cpdlcAssembly.GetType("CPDLCPlugin.Messages.OpenEditorWindowRequest");

        // 13. PluginConfiguration — for reading message templates
        _pluginConfigType = cpdlcAssembly.GetType("CPDLCPlugin.Configuration.PluginConfiguration");
        if (_pluginConfigType != null)
        {
            _uplinkMessagesProp = _pluginConfigType.GetProperty("UplinkMessages");
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
        if (contractsAssembly != null)
        {
            var dialogueDtoType = contractsAssembly.GetType("CPDLCServer.Contracts.DialogueDto");
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

                    var responseName = response.ToString();
                    if (!string.IsNullOrEmpty(responseName)
                        && responseName.IndexOf("WILCO", StringComparison.OrdinalIgnoreCase) >= 0)
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
    /// </summary>
    public static void SendUplink(string callsign, int? replyToDownlinkId, int responseType, string content)
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _sendUplinkRequestType == null || _uplinkResponseTypeEnum == null) return;

            var responseTypeValue = Enum.ToObject(_uplinkResponseTypeEnum, responseType);

            // Construct SendUplinkRequest(string Recipient, int? ReplyToDownlinkId, CpdlcUplinkResponseType ResponseType, string Content)
            var request = Activator.CreateInstance(_sendUplinkRequestType, callsign, replyToDownlinkId, responseTypeValue, content);
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
    public static void SendStandby(int downlinkMessageId, string callsign)
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _sendStandbyRequestType == null) return;

            // Construct SendStandbyUplinkRequest(int DownlinkMessageId, string Recipient)
            var request = Activator.CreateInstance(_sendStandbyRequestType, downlinkMessageId, callsign);
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
    public static void SendUnable(int downlinkMessageId, string callsign, string reason = "")
    {
        if (!IsAvailable) return;

        try
        {
            var mediator = ResolveMediator();
            if (mediator == null || _sendUnableRequestType == null) return;

            var request = Activator.CreateInstance(_sendUnableRequestType, downlinkMessageId, callsign, reason);
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

    private static object? ResolveMediator()
    {
        if (_mediatorType == null) return null;

        var sp = _serviceProviderProp?.GetValue(_cpdlcPlugin) as IServiceProvider;
        return sp?.GetService(_mediatorType);
    }

    private static void InvokeMediatorSend(object mediator, object request)
    {
        // IMediator.Send(IRequest, CancellationToken) — returns Task
        var sendMethod = _mediatorType!.GetMethod("Send", new[] { request.GetType().GetInterfaces().First(i => i.Name.StartsWith("IRequest")), typeof(CancellationToken) });

        // Fallback: find Send method that takes object + CancellationToken
        if (sendMethod == null)
        {
            foreach (var method in _mediatorType.GetMethods())
            {
                if (method.Name != "Send") continue;
                var ps = method.GetParameters();
                if (ps.Length == 2 && ps[1].ParameterType == typeof(CancellationToken))
                {
                    if (ps[0].ParameterType.IsAssignableFrom(request.GetType()) ||
                        ps[0].ParameterType.IsInterface)
                    {
                        sendMethod = method;
                        break;
                    }
                }
            }
        }

        if (sendMethod == null) return;

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
