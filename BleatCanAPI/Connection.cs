using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;

namespace VeadoTube.BleatCan;

public enum ConnectionError : uint
{
    None,
    InvalidServerOrName,
    FailedToConnect,
}

public interface IConnectionReceiver
{
    void OnError(Connection connection, ConnectionError error);
    void OnConnect(Connection connection, bool active);
    void OnReceive(Connection connection, string channel, ReadOnlySpan<byte> data);
}

public class Connection : IDisposable
{
    public readonly string server, name;
    volatile bool active;
    Task task;
    ClientWebSocket client;
    CancellationTokenSource tokenSource;

    readonly Dictionary<string, HashSet<Client>> clients = new();
    bool clientsActive;

    public Connection(string server, string name, IConnectionReceiver receiver = null)
    {
        this.server = server;
        this.name = name;
        var uri = Instance.GetWebSocketUri(server, name);
        if (uri == null)
        {
            receiver?.OnError(this, ConnectionError.InvalidServerOrName);
            return;
        }
        tokenSource = new CancellationTokenSource();
        active = true;
        task = Run(uri, receiver);
    }
    ~Connection() => Dispose();
    public void Dispose()
    {
        if (!active) return;
        active = false;
        tokenSource.Cancel(false);
        task.Wait(200);
        task = null;
        tokenSource = null;
    }

    internal void SetClient(Client client, bool active)
    {
        lock (clients)
        {
            if (active)
            {
                foreach (var i in client.channels)
                {
                    if (!clients.TryGetValue(i, out var set)) clients.Add(i, set = new());
                    set.Add(client);
                }
                if (clientsActive) client.EmitConnect(true);
            }
            else
            {
                foreach (var i in client.channels)
                {
                    if (clients.TryGetValue(i, out var set) && set.Remove(client) && set.Count == 0) clients.Remove(i);
                }
                if (clientsActive) client.EmitConnect(false);
            }
        }
    }

    public bool Send(string channel, ReadOnlySpan<byte> data)
    {
        if (!active || string.IsNullOrEmpty(channel) || client == null) return false;
        byte[] buffer = null;
        bool send;
        try
        {
            int cn = Encoding.UTF8.GetByteCount(channel);
            int n = cn + 1 + data.Length;
            buffer = ArrayPool<byte>.Shared.Rent(n);
            var span = new Span<byte>(buffer, 0, n);
            Encoding.UTF8.GetBytes(channel, span.Slice(0, cn));
            span[cn] = (byte)':';
            data.CopyTo(span.Slice(cn + 1));
            client.SendAsync(new ArraySegment<byte>(buffer, 0, n), WebSocketMessageType.Binary, true, tokenSource.Token).Wait();
            send = true;
        }
        catch
        {
            send = false;
        }
        if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
        return send;
    }

    async Task Run(Uri uri, IConnectionReceiver receiver)
    {
        client = null;
        try
        {
            bool failedToConnect = false;
            bool isConnected = false;

            void UpdateClients()
            {
                lock (clients)
                {
                    clientsActive = isConnected;
                    foreach (var client in clients.Values.SelectMany(x => x).Distinct()) client.EmitConnect(clientsActive);
                }
            }

            var buffer = new List<byte>();
            var readBuffer = new byte[1024];

            while (active)
            {
                client ??= new ClientWebSocket();
                if (client.State != WebSocketState.Open)
                {
                    if (isConnected)
                    {
                        isConnected = false;
                        receiver?.OnConnect(this, false);
                        UpdateClients();
                    }
                    try
                    {
                        await client.ConnectAsync(uri, tokenSource.Token);
                    }
                    catch { }
                    if (client.State != WebSocketState.Open)
                    {
                        client.Abort();
                        client.Dispose();
                        client = null;
                        if (!failedToConnect)
                        {
                            failedToConnect = true;
                            receiver?.OnError(this, ConnectionError.FailedToConnect);
                        }
                        try
                        {
                            await Task.Delay(1000, tokenSource.Token);
                        }
                        catch { }
                        continue;
                    }
                }
                failedToConnect = false;
                if (!isConnected)
                {
                    isConnected = true;
                    receiver?.OnConnect(this, true);
                    UpdateClients();
                    buffer.Clear();
                }
                WebSocketReceiveResult result;
                try
                {
                    result = await client.ReceiveAsync(readBuffer, tokenSource.Token);
                }
                catch
                {
                    continue;
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    try
                    {
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", tokenSource.Token);
                    }
                    catch { }
                    client.Abort();
                    client.Dispose();
                    client = null;
                    continue;
                }
                if (buffer.Count == 0 && result.EndOfMessage && result.Count > 0)
                {
                    Receive(new Span<byte>(readBuffer, 0, result.Count));
                }
                else
                {
                    if (result.Count > 0) buffer.AddRange(new Span<byte>(readBuffer, 0, result.Count));
                    if (result.EndOfMessage && buffer.Count > 0)
                    {
                        Receive(CollectionsMarshal.AsSpan(buffer));
                        buffer.Clear();
                    }
                }
                void Receive(ReadOnlySpan<byte> buffer)
                {
                    int i = buffer.IndexOf((byte)':');
                    if (i < 0) return;
                    string channel;
                    try
                    {
                        channel = Encoding.UTF8.GetString(buffer.Slice(0, i));
                    }
                    catch
                    {
                        channel = null;
                    }
                    if (string.IsNullOrEmpty(channel)) return;
                    var data = buffer.Slice(i + 1);
                    receiver?.OnReceive(this, channel, data);
                    lock (clients)
                    {
                        if (clients.TryGetValue(channel, out var set))
                        {
                            foreach (var client in set) client.EmitReceive(channel, data);
                        }
                    }
                }
            }
            try
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { }
            if (isConnected)
            {
                isConnected = false;
                receiver?.OnConnect(this, false);
                UpdateClients();
            }
        }
        finally
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }
    }
}
