using Newtonsoft.Json;

namespace Deterministic.GameFramework.Core.Data;

public static class GameDataLoader
{
    public static async Task LoadAsync<TEntry>(
        GameData<TEntry> gameData,
        string? basePath = null,
        JsonSerializerSettings? settings = null,
        [System.Runtime.CompilerServices.CallerFilePath] string? callerPath = null)
    {
        // If no basePath provided, find it relative to caller
        if (basePath == null)
        {
            var callerDir = Path.GetDirectoryName(callerPath)!;
            basePath = Path.Combine(callerDir, "Data");
        }
        
        var filePath = Path.Combine(basePath, gameData.Path);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Game data file not found: {filePath}");
        }
        
        var json = await File.ReadAllTextAsync(filePath);
        
        settings ??= JsonSettingsHelper.DefaultSettings;
        
        var entries = JsonConvert.DeserializeObject<Dictionary<string, TEntry>>(json, settings)
            ?? throw new InvalidOperationException($"Failed to deserialize {gameData.Path}");
            
        gameData.Load(entries);
    }
    
    public static async Task LoadAllAsync<TEntry>(
        IEnumerable<GameData<TEntry>> gameDataCollection,
        string basePath,
        JsonSerializerSettings? settings = null)
    {
        var tasks = gameDataCollection.Select(data => LoadAsync(data, basePath, settings));
        await Task.WhenAll(tasks);
    }
    
    public static string FindDataPath(params string[] searchPaths)
    {
        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
                return fullPath;
        }
        
        throw new DirectoryNotFoundException(
            $"Could not find data folder in any of the specified paths: {string.Join(", ", searchPaths)}");
    }
    
    public static string FindDataPathFromAssembly(string relativePath = "GameData/Jsons")
    {
        var assemblyPath = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(assemblyPath, relativePath),
            Path.Combine(assemblyPath, "..", relativePath),
            Path.Combine(assemblyPath, "..", "..", relativePath),
            Path.Combine(assemblyPath, "..", "..", "..", relativePath),
        };
        
        return FindDataPath(possiblePaths);
    }
}
