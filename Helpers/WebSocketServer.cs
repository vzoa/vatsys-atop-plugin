using AtopPlugin.Conflict;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vatsys;

namespace AtopPlugin.Helpers
{
    public class AtopWebSocketServer
    {
        private static AtopWebSocketServer _instance;
        private HttpListener _listener;
        private readonly List<WebSocket> _connectedClients = new List<WebSocket>();
        private readonly int _port;
        private CancellationTokenSource _cts;

        // Event for conflict results received from webapp
        public event Action<List<WebAppConflictResult>> ConflictResultsReceived;

        // Reflection cache for Network methods
        private static readonly Type NetworkType = typeof(Network);
        private static readonly FieldInfo NetworkInstanceField = NetworkType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo SendFlightPlanChangeMethod = NetworkType.GetMethod("SendFlightPlanChange", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo SendNewFlightPlanMethod = NetworkType.GetMethod("SendNewFlightPlan", BindingFlags.NonPublic | BindingFlags.Instance);

        public static AtopWebSocketServer Instance => _instance ?? (_instance = new AtopWebSocketServer());

        private AtopWebSocketServer(int port = 8181)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => StartServerAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            foreach (var client in _connectedClients)
            {
                client?.Dispose();
            }
            _connectedClients.Clear();
        }

        private async Task StartServerAsync(CancellationToken ct)
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();

                while (!ct.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        _connectedClients.Add(wsContext.WebSocket);
                        _ = HandleClientAsync(wsContext.WebSocket, ct);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"WebSocket Server Error: {ex.Message}"));
            }
        }

        private async Task HandleClientAsync(WebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[4096];
            try
            {
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                        _connectedClients.Remove(socket);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleIncomingMessage(message);
                    }
                }
            }
            catch
            {
                _connectedClients.Remove(socket);
            }
        }

        private async Task HandleIncomingMessage(string message)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<WebSocketRequest>(message);

                if (request?.Type == "RequestFDR")
                {
                    var fdrIndex = FDP2.GetFDRIndex(request.Callsign);
                    if (fdrIndex != -1)
                    {
                        await BroadcastFlightPlanDataAsync(FDP2.GetFDRs[fdrIndex]);
                    }
                    else
                    {
                        await BroadcastErrorAsync($"Flight plan not found for '{request.Callsign}'");
                    }
                }
                else if (request?.Type == "UpdateFDR")
                {
                    await HandleFdrUpdate(request);
                }
                else if (request?.Type == "ConflictResults")
                {
                    HandleConflictResults(request);
                }
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"WebSocket Message Error: {ex.Message}"));
            }
        }

        private void HandleConflictResults(WebSocketRequest request)
        {
            if (request.Conflicts == null) return;
            
            var conflicts = request.Conflicts.Select(c => new WebAppConflictResult
            {
                IntruderCallsign = c.IntruderCallsign,
                ActiveCallsign = c.ActiveCallsign,
                Status = c.Status,
                ConflictType = c.ConflictType,
                EarliestLos = c.EarliestLos,
                LatestLos = c.LatestLos,
                LateralSep = c.LateralSep,
                VerticalSep = c.VerticalSep,
                VerticalAct = c.VerticalAct,
                TrkAngle = c.TrkAngle
            }).ToList();
            
            ConflictResultsReceived?.Invoke(conflicts);
        }

        private async Task HandleFdrUpdate(WebSocketRequest request)
        {
            try
            {
                var fdrIndex = FDP2.GetFDRIndex(request.Callsign);
                if (fdrIndex == -1)
                {
                    await BroadcastErrorAsync($"Flight plan not found for '{request.Callsign}'");
                    return;
                }

                var fdr = FDP2.GetFDRs[fdrIndex];

                // Only allow modifications if we have permission
                if (!fdr.HavePermission && fdr.IsTracked)
                {
                    await BroadcastErrorAsync($"No permission to modify '{request.Callsign}'");
                    return;
                }

                // Handle Delete/Cancel action
                if (request.Action == "Delete")
                {
                    FDP2.DeleteFDR(fdr);
                    await BroadcastErrorAsync($"Flight plan '{request.Callsign}' deleted");
                    return;
                }

                // Use FDP2.ModifyFDR for flight plan changes
                if (request.Action == "Modify")
                {
                    FDP2.ModifyFDR(
                        fdr,
                        request.Callsign,
                        request.FlightRules ?? fdr.FlightRules,
                        request.DepAirport ?? fdr.DepAirport,
                        request.DesAirport ?? fdr.DesAirport,
                        request.Route ?? fdr.Route,
                        request.Remarks ?? fdr.Remarks,
                        request.AircraftCount?.ToString() ?? fdr.AircraftCount.ToString(),
                        request.AircraftType ?? fdr.AircraftType,
                        request.AircraftWake ?? fdr.AircraftWake,
                        request.AircraftEquip ?? fdr.AircraftEquip,
                        request.AircraftSurvEquip ?? fdr.AircraftSurvEquip,
                        request.TAS?.ToString() ?? fdr.TAS.ToString(),
                        request.RFL?.ToString() ?? (fdr.RFL / 100).ToString(),
                        fdr.ETD.ToString("HHmm"),
                        fdr.EET.ToString("hhmm"),
                        fdr.ATD != DateTime.MaxValue ? fdr.ATD.ToString("HHmm") : "",
                        request.AltAirport ?? fdr.AltAirport
                    );

                    // Use reflection to call Network.Instance.SendFlightPlanChange
                    SendFlightPlanChangeViaReflection(fdr);
                }
                else if (request.Action == "SetSSR" && request.SSRCode.HasValue)
                {
                    FDP2.SetASSR(fdr, request.SSRCode.Value);
                }
                else if (request.Action == "SetCFL" && !string.IsNullOrEmpty(request.CFL))
                {
                    FDP2.SetCFL(fdr, request.CFL);
                }

                // Broadcast updated FDR back to all clients
                await BroadcastFlightPlanDataAsync(fdr);
            }
            catch (Exception ex)
            {
                await BroadcastErrorAsync($"Update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Uses reflection to call Network.Instance.SendFlightPlanChange(fdr)
        /// </summary>
        private static void SendFlightPlanChangeViaReflection(FDP2.FDR fdr)
        {
            try
            {
                if (NetworkInstanceField == null || SendFlightPlanChangeMethod == null)
                {
                    Errors.Add(new Exception("Unable to find Network.Instance or SendFlightPlanChange method via reflection"));
                    return;
                }

                var networkInstance = NetworkInstanceField.GetValue(null);
                if (networkInstance == null)
                {
                    Errors.Add(new Exception("Network.Instance is null"));
                    return;
                }

                SendFlightPlanChangeMethod.Invoke(networkInstance, new object[] { fdr });
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Reflection error calling SendFlightPlanChange: {ex.Message}"));
            }
        }

        /// <summary>
        /// Uses reflection to call Network.Instance.SendNewFlightPlan(fdr)
        /// </summary>
        private static void SendNewFlightPlanViaReflection(FDP2.FDR fdr)
        {
            try
            {
                if (NetworkInstanceField == null || SendNewFlightPlanMethod == null)
                {
                    Errors.Add(new Exception("Unable to find Network.Instance or SendNewFlightPlan method via reflection"));
                    return;
                }

                var networkInstance = NetworkInstanceField.GetValue(null);
                if (networkInstance == null)
                {
                    Errors.Add(new Exception("Network.Instance is null"));
                    return;
                }

                SendNewFlightPlanMethod.Invoke(networkInstance, new object[] { fdr });
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Reflection error calling SendNewFlightPlan: {ex.Message}"));
            }
        }

        public async Task BroadcastFlightPlanDataAsync(FDP2.FDR fdr)
        {
            if (fdr == null) return;

            var data = new
            {
                Type = "FlightPlanUpdate",
                Callsign = fdr.Callsign,
                FlightRules = fdr.FlightRules,
                AircraftCount = fdr.AircraftCount,
                AircraftType = fdr.AircraftType,
                AircraftWake = fdr.AircraftWake,
                AircraftEquip = fdr.AircraftEquip,
                AircraftSurvEquip = fdr.AircraftSurvEquip,
                DepAirport = fdr.DepAirport,
                DesAirport = fdr.DesAirport,
                AltAirport = fdr.AltAirport,
                ETD = fdr.ETD.ToString("yyyy MMM dd HHmm"),
                ATD = fdr.ATD != DateTime.MaxValue ? fdr.ATD.ToString("HHmm") : "",
                EET = fdr.EET.ToString(@"hhmm"),
                TAS = fdr.TAS,
                RFL = fdr.RFL / 100,
                Route = fdr.Route,
                Remarks = fdr.Remarks,
                SSRCode = fdr.AssignedSSRCode != -1 ? Convert.ToString(fdr.AssignedSSRCode, 8).PadLeft(4, '0') : "",
                LabelOpData = fdr.LabelOpData,
                PRL = fdr.PRL != -1 ? fdr.PRL / 100 : (int?)null,
                CFLString = fdr.CFLString,
                State = fdr.State.ToString(),
                IsTrackedByMe = fdr.IsTrackedByMe,
                HavePermission = fdr.HavePermission,
                ControllingSector = fdr.ControllingSector?.Name,
                RunwayString = fdr.RunwayString,
                SIDSTARString = fdr.SIDSTARString,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastAsync(data);
        }

        public async Task BroadcastAltitudeDataAsync(FDP2.FDR fdr, string selectedAltitude, string responseStatus)
        {
            var data = new
            {
                Type = "AltitudeUpdate",
                Callsign = fdr?.Callsign,
                CurrentLevel = fdr?.PRL,
                CFL = fdr?.CFLString,
                RFL = fdr?.RFL,
                SelectedAltitude = selectedAltitude,
                ResponseStatus = responseStatus,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastAsync(data);
        }

        /// <summary>
        /// Requests a conflict probe from the webapp for a specific callsign
        /// Per ATOP spec 12.1.1, probes are event-driven on FDR updates
        /// </summary>
        public async Task RequestProbeAsync(string callsign = null)
        {
            if (_connectedClients.Count == 0) return;

            var data = new
            {
                Type = "ProbeRequest",
                Callsign = callsign,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastAsync(data);
        }

        public async Task BroadcastConflictDataAsync(ConflictData conflict)
        {
            var data = new
            {
                Type = "ConflictUpdate",
                Status = conflict?.ConflictStatus.ToString(),
                ConflictType = conflict?.ConflictType?.ToString(),
                ActiveCallsign = conflict?.Active?.Callsign,
                IntruderCallsign = conflict?.Intruder?.Callsign,
                EarliestLos = conflict?.EarliestLos,
                LatestLos = conflict?.LatestLos,
                VerticalSep = conflict?.VerticalSep,
                LateralSep = conflict?.LatSep,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastAsync(data);
        }

        private async Task BroadcastErrorAsync(string errorMessage)
        {
            var data = new
            {
                Type = "Error",
                Message = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastAsync(data);
        }

        /// <summary>
        /// Broadcasts all current FDRs to connected clients for conflict calculation
        /// </summary>
        public async Task BroadcastAllFDRsAsync()
        {
            if (_connectedClients.Count == 0) return;
            
            try
            {
                var fdrs = FDP2.GetFDRs.Where(fdr => 
                    fdr.State != FDP2.FDR.FDRStates.STATE_INACTIVE &&
                    fdr.State != FDP2.FDR.FDRStates.STATE_PREACTIVE &&
                    fdr.State != FDP2.FDR.FDRStates.STATE_FINISHED
                ).Select(fdr => new
                {
                    Callsign = fdr.Callsign,
                    State = fdr.State.ToString(),
                    CFL = fdr.CFLUpper,
                    RFL = fdr.RFL / 100,
                    Route = fdr.Route,
                    RouteWaypoints = GetRouteWaypoints(fdr),
                    ATD = fdr.ATD != DateTime.MaxValue ? fdr.ATD.ToString("o") : null,
                    DepAirport = fdr.DepAirport,
                    DesAirport = fdr.DesAirport,
                    AircraftType = fdr.AircraftType,
                    GroundSpeed = fdr.PredictedPosition?.Groundspeed,
                    TAS = fdr.TAS
                }).ToList();
                
                var data = new
                {
                    Type = "FDRBulkUpdate",
                    FDRs = fdrs,
                    Timestamp = DateTime.UtcNow
                };
                
                await BroadcastAsync(data);
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"FDR Bulk Broadcast Error: {ex.Message}"));
            }
        }

        private static object[] GetRouteWaypoints(FDP2.FDR fdr)
        {
            try
            {
                if (fdr.ParsedRoute == null) return new object[0];
                
                return fdr.ParsedRoute.Select(wp => new
                {
                    name = wp.Intersection?.Name ?? "",
                    lat = wp.Intersection?.LatLong.Latitude ?? 0,
                    lon = wp.Intersection?.LatLong.Longitude ?? 0,
                    eto = wp.ETO != DateTime.MaxValue ? wp.ETO.ToString("o") : null
                }).ToArray();
            }
            catch
            {
                return new object[0];
            }
        }

        private async Task BroadcastAsync(object data)
        {
            var json = JsonConvert.SerializeObject(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            var deadClients = new List<WebSocket>();

            foreach (var client in _connectedClients)
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        deadClients.Add(client);
                    }
                }
                catch
                {
                    deadClients.Add(client);
                }
            }

            foreach (var dead in deadClients)
            {
                _connectedClients.Remove(dead);
            }
        }
    }

    public class WebSocketRequest
    {
        public string Type { get; set; }
        public string Action { get; set; }
        public string Callsign { get; set; }
        public string FlightRules { get; set; }
        public int? AircraftCount { get; set; }
        public string AircraftType { get; set; }
        public string AircraftWake { get; set; }
        public string AircraftEquip { get; set; }
        public string AircraftSurvEquip { get; set; }
        public string DepAirport { get; set; }
        public string DesAirport { get; set; }
        public string AltAirport { get; set; }
        public int? TAS { get; set; }
        public int? RFL { get; set; }
        public string Route { get; set; }
        public string Remarks { get; set; }
        public int? SSRCode { get; set; }
        public string CFL { get; set; }
        public List<WebAppConflictResult> Conflicts { get; set; }
    }

    public class WebAppConflictResult
    {
        public string IntruderCallsign { get; set; }
        public string ActiveCallsign { get; set; }
        public string Status { get; set; }
        public string ConflictType { get; set; }
        public string EarliestLos { get; set; }
        public string LatestLos { get; set; }
        public double? LateralSep { get; set; }
        public double? VerticalSep { get; set; }
        public double? VerticalAct { get; set; }
        public double? TrkAngle { get; set; }
    }
}