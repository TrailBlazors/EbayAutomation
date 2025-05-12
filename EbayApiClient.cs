using System.Net.Http.Headers;
using System.Text;
using EbayAutomation.Interfaces;
using Newtonsoft.Json;

namespace EbayAutomation
{
    public class EbayApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IEbayAuthenticationService _authService;
        private string _accessToken = string.Empty; // Initialize to an empty string to satisfy the non-nullable requirement
        private DateTime _tokenExpiration = DateTime.MinValue;

        public EbayApiClient(IEbayAuthenticationService authService, string baseUrl)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        private async Task EnsureAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiration)
            {
                _accessToken = await _authService.GetAccessTokenAsync();
                _tokenExpiration = DateTime.UtcNow.AddMinutes(110); // Tokens typically last 2 hours, setting to 110 minutes for safety
            }
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(content);
        }

        public async Task<T> PostAsync<T>(string endpoint, object data)
        {
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(responseContent);
        }

        public async Task<T> PutAsync<T>(string endpoint, object data)
        {
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(responseContent);
        }

        public async Task DeleteAsync(string endpoint)
        {
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();
        }
    }
}
