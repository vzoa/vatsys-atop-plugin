using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PACOTSPlugin
{
    /// <summary>
    /// Service for interacting with the FAA NMS-API (NOTAM Management System API)
    /// Uses OAuth2 client credentials flow for authentication
    /// </summary>
    public class NotamService
    {
        // Production API Endpoints
        private const string AuthUrl = "https://api-nms.aim.faa.gov/v1/auth/token";
        private const string ApiBaseUrl = "https://api-nms.aim.faa.gov/nmsapi/v1";

        // Hardcoded Credentials
        private const string ClientId = "8DJYfU7tckY2ftGvjR8cHW1YCE62pDjD90GTUokbpbWR3DG2";
        private const string ClientSecret = "uAtU0UqCykWkhesc40xutcFGETbABGwTA0f8mGgHIzlBRMEqpGAj8H0EeFvs9Dk0";

        private readonly HttpClient _httpClient;

        // Token caching
        private string _cachedToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        // Token expires in 1799 seconds (30 minutes), refresh 5 minutes early
        private const int TokenRefreshBufferSeconds = 300;

        public NotamService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        #region Authentication

        /// <summary>
        /// Retrieves a bearer token using OAuth2 client credentials flow
        /// </summary>
        public async Task<string> GetBearerTokenAsync()
        {
            await _tokenLock.WaitAsync();
            try
            {
                // Return cached token if still valid
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
                {
                    return _cachedToken;
                }

                // Request new token
                var tokenResponse = await RequestNewTokenAsync();
                
                _cachedToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - TokenRefreshBufferSeconds);

                return _cachedToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<TokenResponse> RequestNewTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, AuthUrl);
            
            // Set Basic Auth header with client credentials
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // Set form content for client_credentials grant
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new NotamApiException($"Failed to obtain bearer token. Status: {response.StatusCode}, Error: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(content);

            if (string.IsNullOrEmpty(tokenResponse?.AccessToken))
            {
                throw new NotamApiException("Token response did not contain an access token");
            }

            return tokenResponse;
        }

        #endregion

        #region API Methods

        /// <summary>
        /// Retrieves NOTAMs by location (ICAO identifier)
        /// </summary>
        /// <param name="location">ICAO location identifier (e.g., "KZAK", "PHNL")</param>
        public async Task<NmsNotamResponse> GetNotamsByLocationAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentException("Location is required", nameof(location));
            }

            return await SendNotamRequestAsync($"/notams?location={location}");
        }

        /// <summary>
        /// Retrieves NOTAMs updated since a specific date (up to 24 hours back)
        /// </summary>
        /// <param name="lastUpdatedDate">UTC timestamp to search from</param>
        public async Task<NmsNotamResponse> GetNotamsByLastUpdatedAsync(DateTime lastUpdatedDate)
        {
            var dateStr = lastUpdatedDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
            return await SendNotamRequestAsync($"/notams?lastUpdatedDate={dateStr}");
        }

        /// <summary>
        /// Retrieves NOTAMs by geospatial search
        /// </summary>
        /// <param name="latitude">Latitude in decimal degrees (-90 to 90)</param>
        /// <param name="longitude">Longitude in decimal degrees (-180 to 180)</param>
        /// <param name="radiusNm">Radius in nautical miles (0 to 100)</param>
        public async Task<NmsNotamResponse> GetNotamsByLocationRadiusAsync(double latitude, double longitude, double radiusNm)
        {
            return await SendNotamRequestAsync($"/notams?latitude={latitude}&longitude={longitude}&radius={radiusNm}");
        }

        /// <summary>
        /// Retrieves NOTAMs by free text search
        /// </summary>
        /// <param name="searchText">Text to search for (max 80 chars)</param>
        public async Task<NmsNotamResponse> GetNotamsByFreeTextAsync(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                throw new ArgumentException("Search text is required", nameof(searchText));
            }

            var encodedText = Uri.EscapeDataString(searchText);
            return await SendNotamRequestAsync($"/notams?freeText={encodedText}");
        }

        /// <summary>
        /// Retrieves NOTAMs by classification
        /// </summary>
        /// <param name="classification">INTERNATIONAL, MILITARY, LOCAL_MILITARY, DOMESTIC, or FDC</param>
        public async Task<NmsNotamResponse> GetNotamsByClassificationAsync(string classification)
        {
            if (string.IsNullOrEmpty(classification))
            {
                throw new ArgumentException("Classification is required", nameof(classification));
            }

            return await SendNotamRequestAsync($"/notams?classification={classification}");
        }

        /// <summary>
        /// Retrieves NOTAMs by feature type
        /// </summary>
        /// <param name="feature">RWY, TWY, APRON, AD, OBST, NAV, COM, SVC, AIRSPACE, etc.</param>
        public async Task<NmsNotamResponse> GetNotamsByFeatureAsync(string feature)
        {
            if (string.IsNullOrEmpty(feature))
            {
                throw new ArgumentException("Feature is required", nameof(feature));
            }

            return await SendNotamRequestAsync($"/notams?feature={feature}");
        }

        /// <summary>
        /// Searches for PACOTS-related NOTAMs from both RJJJ (eastbound) and KZAK (westbound)
        /// </summary>
        public async Task<NmsNotamResponse> GetPacotsNotamsAsync()
        {
            // PACOTS tracks are filed under:
            // - RJJJ (Japan FIR) for eastbound tracks (Japan -> North America)
            // - KZAK (Oakland Oceanic) for westbound tracks (North America -> Japan)
            
            // Sequential requests to avoid rate limiting (API allows 1 req/sec)
            var eastbound = await SendNotamRequestAsync("/notams?location=RJJJ");
            await Task.Delay(1100); // Wait 1.1 seconds between requests
            var westbound = await SendNotamRequestAsync("/notams?location=KZAK");
            
            // Combine results
            var combined = new NmsNotamResponse
            {
                Status = "Success",
                Data = new NmsNotamData
                {
                    GeoJson = new List<NotamGeoJson>()
                }
            };
            
            if (eastbound?.Data?.GeoJson != null)
            {
                combined.Data.GeoJson.AddRange(eastbound.Data.GeoJson);
            }
            
            if (westbound?.Data?.GeoJson != null)
            {
                combined.Data.GeoJson.AddRange(westbound.Data.GeoJson);
            }
            
            return combined;
        }

        /// <summary>
        /// Gets NOTAMs for the Oakland Oceanic FIR area using geospatial search
        /// Center point approximately in the middle of KZAK FIR
        /// </summary>
        public async Task<NmsNotamResponse> GetOceanicNotamsAsync()
        {
            // Center of Pacific - roughly mid-way between Hawaii and Japan
            // Using a large radius to capture oceanic NOTAMs
            return await SendNotamRequestAsync($"/notams?latitude=30&longitude=-160&radius=100");
        }

        private async Task<NmsNotamResponse> SendNotamRequestAsync(string endpoint)
        {
            var token = await GetBearerTokenAsync();
            
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}{endpoint}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Required header for NMS-API - use GEOJSON for easier parsing
            request.Headers.Add("nmsResponseFormat", "GEOJSON");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new NotamApiException($"Failed to retrieve NOTAMs. Status: {response.StatusCode}, Error: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            
            try
            {
                var nmsResponse = JsonConvert.DeserializeObject<NmsNotamResponse>(content);
                return nmsResponse ?? new NmsNotamResponse();
            }
            catch (JsonReaderException ex)
            {
                throw new NotamApiException($"Failed to parse NOTAM response: {ex.Message}. Content: {content.Substring(0, Math.Min(500, content.Length))}");
            }
        }

        #endregion

        /// <summary>
        /// Invalidates the cached token, forcing a refresh on next request
        /// </summary>
        public void InvalidateToken()
        {
            _tokenLock.Wait();
            try
            {
                _cachedToken = null;
                _tokenExpiry = DateTime.MinValue;
            }
            finally
            {
                _tokenLock.Release();
            }
        }
    }

    #region Response Models

    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public string ExpiresInStr { get; set; }
        
        public int ExpiresIn => int.TryParse(ExpiresInStr, out var val) ? val : 1799;

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("issued_at")]
        public string IssuedAt { get; set; }

        [JsonProperty("client_id")]
        public string ClientId { get; set; }
    }

    /// <summary>
    /// NMS API Response wrapper
    /// </summary>
    public class NmsNotamResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("errors")]
        public List<NmsError> Errors { get; set; }

        [JsonProperty("data")]
        public NmsNotamData Data { get; set; }
    }

    public class NmsError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class NmsNotamData
    {
        [JsonProperty("geojson")]
        public List<NotamGeoJson> GeoJson { get; set; }

        [JsonProperty("aixm")]
        public List<string> Aixm { get; set; }
    }

    /// <summary>
    /// GeoJSON Feature for NOTAM
    /// </summary>
    public class NotamGeoJson
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("properties")]
        public NotamProperties Properties { get; set; }

        [JsonProperty("geometry")]
        public NotamGeometry Geometry { get; set; }
    }

    public class NotamProperties
    {
        [JsonProperty("coreNOTAMData")]
        public CoreNotamData CoreNotamData { get; set; }
    }

    public class CoreNotamData
    {
        [JsonProperty("notam")]
        public NotamDetail Notam { get; set; }

        [JsonProperty("notamTranslation")]
        public List<NotamTranslation> NotamTranslation { get; set; }
    }

    public class NotamDetail
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("series")]
        public string Series { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }

        [JsonProperty("year")]
        public string Year { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("issued")]
        public string Issued { get; set; }

        [JsonProperty("affectedFir")]
        public string AffectedFir { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("icaoLocation")]
        public string IcaoLocation { get; set; }

        [JsonProperty("effectiveStart")]
        public string EffectiveStart { get; set; }

        [JsonProperty("effectiveEnd")]
        public string EffectiveEnd { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("classification")]
        public string Classification { get; set; }

        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; }

        [JsonProperty("schedule")]
        public string Schedule { get; set; }

        [JsonProperty("selectionCode")]
        public string SelectionCode { get; set; }

        [JsonProperty("traffic")]
        public string Traffic { get; set; }

        [JsonProperty("purpose")]
        public string Purpose { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("minimumFl")]
        public string MinimumFl { get; set; }

        [JsonProperty("maximumFl")]
        public string MaximumFl { get; set; }

        [JsonProperty("coordinates")]
        public string Coordinates { get; set; }

        [JsonProperty("radius")]
        public string Radius { get; set; }
    }

    public class NotamTranslation
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("domestic_message")]
        public string DomesticMessage { get; set; }

        [JsonProperty("icao_message")]
        public string IcaoMessage { get; set; }
    }

    public class NotamGeometry
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("coordinates")]
        public JToken Coordinates { get; set; }

        [JsonProperty("geometries")]
        public List<NotamGeometry> Geometries { get; set; }
    }

    #endregion

    #region Simplified NOTAM model for internal use

    /// <summary>
    /// Simplified NOTAM model for plugin use
    /// </summary>
    public class Notam
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public string Location { get; set; }
        public string IcaoLocation { get; set; }
        public string AffectedFir { get; set; }
        public DateTime? EffectiveStart { get; set; }
        public DateTime? EffectiveEnd { get; set; }
        public string Text { get; set; }
        public string Classification { get; set; }
        public string DomesticMessage { get; set; }
        public string IcaoMessage { get; set; }
        public string TraditionalMessage { get; set; }

        public bool IsActive => EffectiveStart <= DateTime.UtcNow && (EffectiveEnd == null || EffectiveEnd >= DateTime.UtcNow);
        
        public string DisplayText => !string.IsNullOrEmpty(DomesticMessage) ? DomesticMessage : 
                                     !string.IsNullOrEmpty(Text) ? Text : IcaoMessage;

        /// <summary>
        /// Creates a simplified Notam from the GeoJSON response
        /// </summary>
        public static Notam FromGeoJson(NotamGeoJson geoJson)
        {
            var detail = geoJson?.Properties?.CoreNotamData?.Notam;
            if (detail == null) return null;

            var notam = new Notam
            {
                Id = detail.Id,
                Number = detail.Number,
                Location = detail.Location,
                IcaoLocation = detail.IcaoLocation,
                AffectedFir = detail.AffectedFir,
                Text = detail.Text,
                Classification = detail.Classification
            };

            // Parse dates
            if (DateTime.TryParse(detail.EffectiveStart, out var startDate))
                notam.EffectiveStart = startDate;
            if (DateTime.TryParse(detail.EffectiveEnd, out var endDate))
                notam.EffectiveEnd = endDate;

            // Get translations
            var translations = geoJson.Properties?.CoreNotamData?.NotamTranslation;
            if (translations != null)
            {
                foreach (var trans in translations)
                {
                    if (trans.Type == "LOCAL_FORMAT")
                        notam.DomesticMessage = trans.DomesticMessage;
                    else if (trans.Type == "ICAO")
                        notam.IcaoMessage = trans.IcaoMessage;
                }
            }

            return notam;
        }

        /// <summary>
        /// Converts NMS API response to list of simplified Notams
        /// </summary>
        public static List<Notam> FromResponse(NmsNotamResponse response)
        {
            var notams = new List<Notam>();
            
            if (response?.Data?.GeoJson == null)
                return notams;

            foreach (var geoJson in response.Data.GeoJson)
            {
                var notam = FromGeoJson(geoJson);
                if (notam != null)
                    notams.Add(notam);
            }

            return notams;
        }
    }

    #endregion

    #region Exceptions

    public class NotamApiException : Exception
    {
        public NotamApiException(string message) : base(message) { }
        public NotamApiException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion
}
