using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Serialization;

namespace Deterministic.GameFramework.Debugging;

public class DesyncRecorder : IDisposable
{
    private readonly GameSimulation _simulation;
    private StreamWriter? _writer;
    private bool _running;
    private string _side = "unknown";
    private Guid _prevHash;
    private int _snapInterval = 60;

    private readonly Dictionary<long, List<ActionEntry>> _actionsByTick = new();

    private struct ActionEntry
    {
        public string TypeName;
        public int TargetEntityId;
        public string DataHash;
    }

    public DesyncRecorder(GameSimulation simulation)
    {
        _simulation = simulation;
    }

    public void Start(string outputPath, string side, string sessionId, int tickRate = 60, int maxLogs = 3, int snapInterval = 60)
    {
        if (_running) return;
        _side = side;
        _snapInterval = snapInterval;

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            CleanupOldLogs(dir, $"{side}_", maxLogs);
        }

        _writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(false))
        {
            AutoFlush = false
        };

        _writer.WriteLine($"{{\"session\":\"{sessionId}\",\"side\":\"{side}\",\"tickRate\":{tickRate}}}");

        _simulation.Scheduler.OnActionScheduled += OnActionScheduled;
        _simulation.Scheduler.OnActionRejected += OnActionRejected;
        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        _simulation.Scheduler.OnActionScheduled -= OnActionScheduled;
        _simulation.Scheduler.OnActionRejected -= OnActionRejected;
        _actionsByTick.Clear();

        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
    }

    public void Dispose() => Stop();

    public bool RecordResimTicks { get; set; } = false;

    public void RecordTick(long tick, bool isResim, byte[]? preSerializedSnapshot = null)
    {
        if (!_running || _writer == null) return;
        if (isResim && !RecordResimTicks) return;

        Guid hash;
        if (preSerializedSnapshot != null)
            hash = StateHasher.Hash(preSerializedSnapshot);
        else
            hash = StateHasher.Hash(_simulation.State);

        int nextEntityId = _simulation.State.NextEntityId;

        var actionsSb = new StringBuilder();
        actionsSb.Append('[');
        if (_actionsByTick.TryGetValue(tick, out var actions))
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (i > 0) actionsSb.Append(',');
                var a = actions[i];
                actionsSb.Append($"{{\"t\":\"{EscapeJson(a.TypeName)}\",\"e\":{a.TargetEntityId},\"d\":\"{a.DataHash}\"}}");
            }
            _actionsByTick.Remove(tick);
        }
        actionsSb.Append(']');

        string snapshotField = "";
        bool shouldSnap = (tick % _snapInterval == 0);
        if (shouldSnap && preSerializedSnapshot != null)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest))
            {
                gz.Write(preSerializedSnapshot, 0, preSerializedSnapshot.Length);
            }
            snapshotField = $",\"snap\":\"{Convert.ToBase64String(ms.ToArray())}\"";
        }
        _prevHash = hash;

        _writer.WriteLine($"{{\"tick\":{tick},\"hash\":\"{hash}\",\"eid\":{nextEntityId},\"actions\":{actionsSb},\"resim\":{(isResim ? "true" : "false")}{snapshotField}}}");

        if (tick % 60 == 0)
            _writer.Flush();
    }

    private void OnActionRejected(DenseComponentId id, ReadOnlySpan<byte> data, int targetEntityId, long executeTick, long minAllowedTick)
    {
        if (!_running || _writer == null) return;

        string typeName = ComponentId.TryGetType(id, out var type) && type != null
            ? type.Name
            : $"CID({id.Value})";

        string dataHash;
        var dataArray = data.ToArray();
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(dataArray);
            dataHash = BitConverter.ToString(hashBytes, 0, 4).Replace("-", "");
        }

        _writer.WriteLine($"{{\"rejected\":true,\"executeTick\":{executeTick},\"minAllowed\":{minAllowedTick},\"action\":\"{EscapeJson(typeName)}\",\"target\":{targetEntityId},\"data\":\"{dataHash}\"}}");
        _writer.Flush();
    }

    private void OnActionScheduled(DenseComponentId id, ReadOnlySpan<byte> data, int targetEntityId, long executeTick, long originalExecuteTick, long predictionId)
    {
        if (!_running) return;

        string typeName = ComponentId.TryGetType(id, out var type) && type != null
            ? type.Name
            : $"CID({id.Value})";

        string dataHash;
        var dataArray = data.ToArray();
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(dataArray);
            dataHash = BitConverter.ToString(hashBytes, 0, 4).Replace("-", "");
        }

        if (!_actionsByTick.TryGetValue(executeTick, out var list))
        {
            list = new List<ActionEntry>(4);
            _actionsByTick[executeTick] = list;
        }

        list.Add(new ActionEntry
        {
            TypeName = typeName,
            TargetEntityId = targetEntityId,
            DataHash = dataHash
        });
    }

    private static void CleanupOldLogs(string directory, string prefix, int keepCount)
    {
        if (keepCount <= 0) return;

        try
        {
            var files = Directory.GetFiles(directory, $"{prefix}*.jsonl");
            if (files.Length <= keepCount) return;

            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));

            int toDelete = files.Length - keepCount;
            for (int i = 0; i < toDelete; i++)
            {
                try { File.Delete(files[i]); }
                catch { }
            }
        }
        catch { }
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
