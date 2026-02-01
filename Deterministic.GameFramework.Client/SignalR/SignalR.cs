using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Client
{
    public class ConnectionEventArgs : EventArgs
    {
        public string ConnectionId { get; set; }
    }

    public class SignalR
{
    private static SignalR instance;
    
    public SignalR()
    {
        instance = this;
    }

    private static void OnConnectionStarted(string connectionId)
    {
        var args = new ConnectionEventArgs
        {
            ConnectionId = connectionId
        };
        instance.ConnectionStarted?.Invoke(instance, args);
    }
    public event EventHandler<ConnectionEventArgs> ConnectionStarted;

    private static void OnConnectionClosed(string connectionId)
    {
        var args = new ConnectionEventArgs
        {
            ConnectionId = connectionId
        };

        instance.ConnectionClosed?.Invoke(instance, args);
    }
    public event EventHandler<ConnectionEventArgs> ConnectionClosed;

    private HubConnection connection;
    private static string lastConnectionId;

    public void Init(string url)
    {
        try
        {
            connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .WithServerTimeout(TimeSpan.FromMinutes(1))
            .WithKeepAliveInterval(TimeSpan.FromSeconds(15))
            .Build();
        }
        catch (Exception ex)
        {
            NetworkLogger.LogError($"[SignalR] Init error: {ex.Message}");
        }
    }

    public async void Connect()
    {
        try
        {
            await connection.StartAsync();

            lastConnectionId = connection.ConnectionId;

            connection.Closed -= OnConnectionClosedEvent;
            connection.Reconnecting -= OnConnectionReconnectingEvent;
            connection.Reconnected -= OnConnectionReconnectedEvent;

            connection.Closed += OnConnectionClosedEvent;
            connection.Reconnecting += OnConnectionReconnectingEvent;
            connection.Reconnected += OnConnectionReconnectedEvent;

            OnConnectionStarted(lastConnectionId);
        }
        catch (Exception ex)
        {
            NetworkLogger.LogError($"[SignalR] Connect error: {ex}");
        }
    }

    public async void Stop()
    {
        try
        {
            await connection.StopAsync();
        }
        catch (Exception e)
        {
            NetworkLogger.LogError($"[SignalR] Stop error: {e}");
        }
    }

    private static Task OnConnectionClosedEvent(Exception exception)
    {
        if (exception != null)
        {
            NetworkLogger.LogError($"[SignalR] Connection closed: {exception.Message}");
        }

        OnConnectionClosed(lastConnectionId);

        return Task.CompletedTask;
    }

    private static Task OnConnectionReconnectingEvent(Exception exception)
    {
        NetworkLogger.Log($"[SignalR] Reconnecting due to error: {exception.Message}");

        return Task.CompletedTask;
    }

    private static Task OnConnectionReconnectedEvent(string connectionId)
    {
        NetworkLogger.Log($"[SignalR] Reconnected. ConnectionId: {connectionId}");

        lastConnectionId = connectionId;

        OnConnectionStarted(lastConnectionId);

        return Task.CompletedTask;
    }

    #region Invoke Editor
    public async void Invoke(string methodName) =>
        await connection.InvokeAsync(methodName);
    public async void Invoke(string methodName, object arg1) =>
        await connection.InvokeAsync(methodName, arg1);
    public async void Invoke(string methodName, object arg1, object arg2) =>
        await connection.InvokeAsync(methodName, arg1, arg2);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4, object arg5) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4, arg5);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4, arg5, arg6);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    public async void Invoke(string methodName, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9, object arg10) =>
        await connection.InvokeAsync(methodName, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    #endregion

    #region On Editor
    public IDisposable On(string methodName, Action handler) =>
        connection.On(methodName, handler.Invoke);
    public IDisposable On<T1>(string methodName, Action<T1> handler) =>
        connection.On(methodName, (T1 arg1) => handler.Invoke(arg1));
    public IDisposable On<T1, T2>(string methodName, Action<T1, T2> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2) => handler.Invoke(arg1, arg2));
    public IDisposable On<T1, T2, T3>(string methodName, Action<T1, T2, T3> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2, T3 arg3) => handler.Invoke(arg1, arg2, arg3));
    public IDisposable On<T1, T2, T3, T4>(string methodName, Action<T1, T2, T3, T4> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2, T3 arg3, T4 arg4) => handler.Invoke(arg1, arg2, arg3, arg4));
    public IDisposable On<T1, T2, T3, T4, T5>(string methodName, Action<T1, T2, T3, T4, T5> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) => handler.Invoke(arg1, arg2, arg3, arg4, arg5));
    public IDisposable On<T1, T2, T3, T4, T5, T6>(string methodName, Action<T1, T2, T3, T4, T5, T6> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) => handler.Invoke(arg1, arg2, arg3, arg4, arg5, arg6));
    public IDisposable On<T1, T2, T3, T4, T5, T6, T7>(string methodName, Action<T1, T2, T3, T4, T5, T6, T7> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7) => handler.Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7));
    public IDisposable On<T1, T2, T3, T4, T5, T6, T7, T8>(string methodName, Action<T1, T2, T3, T4, T5, T6, T7, T8> handler) =>
        connection.On(methodName, (T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8) => handler.Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
    #endregion
    }
}
