using Deterministic.GameFramework.Server;
using MultiplayerHelloWorld.Shared;

// Optional: Enable strict whitelist mode if needed for security
// JsonSerializerConfig.EnableStrictWhitelist();

var builder = WebApplication.CreateBuilder(args);

// Add multiplayer server with factory function (includes ServerDomain, MatchManager, Factory, SignalR)
builder.Services.AddMultiplayerServer<CounterGameState>(matchId => new CounterGameState(matchId));

// Add CORS for client connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

// Map the default GameHub endpoint
app.MapHub<DefaultGameHub<CounterGameState>>("/gamehub");

app.MapGet("/", () => "Multiplayer Counter Server - Connect to /gamehub");

Console.WriteLine("Server running on http://localhost:5000");
Console.WriteLine("Clients can connect to: http://localhost:5000/gamehub");

app.Run();
