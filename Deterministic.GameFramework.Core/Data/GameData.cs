namespace Deterministic.GameFramework.Core.Data;

public abstract class GameData<TEntry>
{
    public abstract string Path { get; }
    public abstract void Load(Dictionary<string, TEntry> entries);
}

public abstract class GameData<TEntry, TModel> : GameData<TEntry>
{
    private Dictionary<string, TModel> _data = new();
    
    public override void Load(Dictionary<string, TEntry> entries)
    {
        _data = entries.Values
            .Where(e => GetKey(e) != null)
            .GroupBy(GetKey)
            .ToDictionary(g => g.Key!, CreateModel);
    }
    
    public TModel? Get(string key) => _data.GetValueOrDefault(key);
    
    public Dictionary<string, TModel> GetAll() => _data;
    
    protected abstract string GetKey(TEntry entry);
    protected abstract TModel CreateModel(IGrouping<string, TEntry> entries);
}
