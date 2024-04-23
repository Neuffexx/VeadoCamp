namespace VeadoTube.BleatCan;

public struct Instance
{
    public InstanceID id;
    public string name, server;

    public bool PropertiesEqual(Instance i) => name == i.name && server == i.server;

    public Uri GetWebSocketUri(string name) => GetWebSocketUri(server, name);
    public static Uri GetWebSocketUri(string server, string name)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            return new Uri($"ws://{server.Trim()}?n={Uri.EscapeDataString(name.Trim())}");
        }
        catch
        {
            return null;
        }
    }

    public Connection Connect(string name, IConnectionReceiver receiver) => new Connection(name, server, receiver);
}
