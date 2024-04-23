using System.Text.Json;
using System.Text.Json.Nodes;

namespace VeadoTube.BleatCan;

public interface IInstancesReceiver
{
    void OnStart(Instance instance);
    void OnChange(Instance instance);
    void OnEnd(InstanceID id);
}

public class Instances : IDisposable
{
    volatile bool active;
    Task task;
    CancellationTokenSource tokenSource;

    public Instances(IInstancesReceiver receiver)
    {
        tokenSource = new CancellationTokenSource();
        active = true;
        task = Run(receiver);
    }
    ~Instances() => Dispose();
    public void Dispose()
    {
        if (!active) return;
        active = false;
        tokenSource.Cancel(false);
        task.Wait(200);
        task = null;
        tokenSource = null;
    }

    static readonly string directory = GetDirectory();
    static string GetDirectory()
    {
        var path = Path.Combine(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), ".veadotube", "instances");
        try
        {
            Directory.CreateDirectory(path);
        }
        catch { }
        return path;
    }
    static long unixTime => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    const long readTimeout = 10;

    static InstanceID ParseInstanceID(string path) => new InstanceID(Path.GetFileName(path));
    static bool ReadInstance(string path, InstanceID id, out Instance instance, out long time)
    {
        instance = new Instance { id = id };
        time = 0;
        if (!instance.id.isValid) return false;
        try
        {
            var reader = new Utf8JsonReader(File.ReadAllBytes(path));
            if (JsonNode.Parse(ref reader) is not JsonObject obj) return false;
            if (!obj.TryGetProperty("time", out time) || time < unixTime - readTimeout) return false;
            if (!obj.TryGetProperty("name", out instance.name)) instance.name = null;
            if (!obj.TryGetProperty("server", out instance.server)) instance.server = null;
            instance.name ??= "";
            if (string.IsNullOrWhiteSpace(instance.server))
            {
                instance.server = null;
            }
            else
            {
                instance.server = instance.server.Trim();
            }
        }
        catch
        {
            return false;
        }
        return true;
    }

    async Task Run(IInstancesReceiver receiver)
    {
        var instances = new Dictionary<InstanceID, (Instance instance, long time)>();

        using var watcher = new FileSystemWatcher(directory);
        watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName;
        watcher.Changed += (o, e) => OnFileChange(true, false, e.FullPath);
        watcher.Created += (o, e) => OnFileChange(true, false, e.FullPath);
        watcher.Deleted += (o, e) => OnFileChange(false, false, e.FullPath);
        watcher.Filter = "*";
        watcher.EnableRaisingEvents = true;

        foreach (var i in Directory.EnumerateFiles(directory)) OnFileChange(true, true, i);

        void OnFileChange(bool active, bool first, string path)
        {
            if (!this.active) return;
            var id = ParseInstanceID(path);
            if (!id.isValid) return;
            lock (instances)
            {
                if (active && ReadInstance(path, id, out var instance, out long time))
                {
                    if (!instances.TryGetValue(id, out var x))
                    {
                        receiver.OnStart(instance);
                    }
                    else if (!x.instance.PropertiesEqual(instance))
                    {
                        receiver.OnChange(instance);
                    }
                    instances[id] = (instance, time);
                }
                else if (active && !first)
                {
                    if (instances.TryGetValue(id, out var x))
                    {
                        instances[id] = (x.instance, unixTime);
                    }
                }
                else if (instances.Remove(id))
                {
                    receiver.OnEnd(id);
                }
            }
        }

        while (true)
        {
            try
            {
                await Task.Delay(1000, tokenSource.Token);
            }
            catch { }
            if (!active) break;
            lock (instances)
            {
                HashSet<InstanceID> remove = null;
                foreach (var (instance, time) in instances.Values)
                {
                    if (time < unixTime - readTimeout)
                    {
                        (remove ??= new()).Add(instance.id);
                    }
                }
                if (remove != null)
                {
                    foreach (var id in remove)
                    {
                        instances.Remove(id);
                        receiver.OnEnd(id);
                    }
                }
            }
        }

        watcher.EnableRaisingEvents = false;
        foreach (var id in instances.Keys) receiver.OnEnd(id);
    }

    public static IEnumerable<Instance> Enumerate()
    {
        foreach (var i in Directory.EnumerateFiles(directory))
        {
            if (ReadInstance(i, ParseInstanceID(i), out var instance, out _)) yield return instance;
        }
    }
}