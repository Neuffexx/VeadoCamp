namespace VeadoTube.BleatCan;

public abstract class Client : IDisposable
{
    Connection connection;
    internal readonly string[] channels;

    public Client(Connection connection, string channel) : this(connection, [channel]) { }
    public Client(Connection connection, IEnumerable<string> channels)
    {
        this.connection = connection;
        this.channels = channels.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
        connection?.SetClient(this, true);
    }
    ~Client() => Dispose();
    public void Dispose()
    {
        if (connection == null) return;
        connection.SetClient(this, false);
        connection = null;
    }

    internal void EmitReceive(string channel, ReadOnlySpan<byte> data) => OnReceive(channel, data);
    internal void EmitConnect(bool active) => OnConnect(active);

    protected void Send(ReadOnlySpan<byte> data) => connection?.Send(channels.FirstOrDefault(), data);
    protected void Send(string channel, ReadOnlySpan<byte> data) => connection?.Send(channel, data);

    protected virtual void OnConnect(bool active) { }
    protected virtual void OnReceive(string channel, ReadOnlySpan<byte> data) { }
}
