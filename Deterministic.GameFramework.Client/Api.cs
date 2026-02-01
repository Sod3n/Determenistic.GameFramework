using System;
using System.Threading.Tasks;
using RestSharp;

namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// REST API client for making HTTP requests to the server.
    /// Extend this class with your game-specific API endpoints.
    /// Engine-agnostic - works with Godot, Unity, MonoGame, etc.
    /// Uses RestSharp for HTTP requests.
    /// </summary>
    public class Api
    {
        private readonly RestClient _client;
        
        public string BaseUrl { get; }
        
        public Api(string baseUrl)
        {
            BaseUrl = baseUrl;
            _client = new RestClient(baseUrl);
        }

        // GET request
        public async Task<T> Get<T>(string url)
        {
            var request = new RestRequest(url, Method.Get);
            NetworkLogger.Log($"[API] GET {BaseUrl}{url}");
            
            try
            {
                var response = await _client.ExecuteAsync<T>(request);
                
                if (!response.IsSuccessful)
                {
                    NetworkLogger.LogError($"[API] Error: {response.StatusCode} - {response.ErrorMessage}");
                    return default;
                }
                
                return response.Data;
            }
            catch (Exception ex)
            {
                NetworkLogger.LogError($"[API] Exception: {ex.Message}");
                return default;
            }
        }

        // POST request
        public async Task<T> Post<T>(string url, object body = null)
        {
            var request = new RestRequest(url, Method.Post);
            if (body != null)
                request.AddJsonBody(body);
                
            NetworkLogger.Log($"[API] POST {BaseUrl}{url}");
            
            try
            {
                var response = await _client.ExecuteAsync<T>(request);
                
                if (!response.IsSuccessful)
                {
                    NetworkLogger.LogError($"[API] Error: {response.StatusCode} - {response.ErrorMessage}");
                    return default;
                }
                
                return response.Data;
            }
            catch (Exception ex)
            {
                NetworkLogger.LogError($"[API] Exception: {ex.Message}");
                return default;
            }
        }

        // PUT request
        public async Task<T> Put<T>(string url, object body = null)
        {
            var request = new RestRequest(url, Method.Put);
            if (body != null)
                request.AddJsonBody(body);
                
            NetworkLogger.Log($"[API] PUT {BaseUrl}{url}");
            
            try
            {
                var response = await _client.ExecuteAsync<T>(request);
                
                if (!response.IsSuccessful)
                {
                    NetworkLogger.LogError($"[API] Error: {response.StatusCode} - {response.ErrorMessage}");
                    return default;
                }
                
                return response.Data;
            }
            catch (Exception ex)
            {
                NetworkLogger.LogError($"[API] Exception: {ex.Message}");
                return default;
            }
        }

        // DELETE request
        public async Task<T> Delete<T>(string url)
        {
            var request = new RestRequest(url, Method.Delete);
            NetworkLogger.Log($"[API] DELETE {BaseUrl}{url}");
            
            try
            {
                var response = await _client.ExecuteAsync<T>(request);
                
                if (!response.IsSuccessful)
                {
                    NetworkLogger.LogError($"[API] Error: {response.StatusCode} - {response.ErrorMessage}");
                    return default;
                }
                
                return response.Data;
            }
            catch (Exception ex)
            {
                NetworkLogger.LogError($"[API] Exception: {ex.Message}");
                return default;
            }
        }
    }
}
