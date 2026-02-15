using System.Threading.Tasks;

namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// Interface for REST API client implementations.
    /// Each engine (Unity, Godot, etc.) can provide its own implementation.
    /// </summary>
    public interface IApi
    {
        string BaseUrl { get; }
        
        Task<T> Get<T>(string url);
        Task<T> Post<T>(string url, object body = null);
        Task<T> Put<T>(string url, object body = null);
        Task<T> Delete<T>(string url);
    }
}
