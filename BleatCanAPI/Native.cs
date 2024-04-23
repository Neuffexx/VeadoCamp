using System.Runtime.InteropServices;
using System.Text;

namespace VeadoTube.BleatCan;

internal static unsafe class Native
{
    static bool FromString(string i, byte* ptr, int n) => FromString(i, new Span<byte>(ptr, n));
    static bool FromString(string i, Span<byte> o)
    {
        try
        {
            if (string.IsNullOrEmpty(i))
            {
                o.Clear();
                return false;
            }
            int n = Encoding.UTF8.GetByteCount(i);
            if (n >= o.Length - 1)
            {
                o.Clear();
                return false;
            }
            Encoding.UTF8.GetBytes(i, o);
            o.Slice(n).Clear();
            return true;
        }
        catch
        {
            o.Clear();
            return false;
        }
    }
    static byte[] FromString(string i)
    {
        if (string.IsNullOrEmpty(i)) return [0];
        try
        {
            return Encoding.UTF8.GetBytes(i + '\0');
        }
        catch
        {
            return [0];
        }
    }
    static string ToString(byte* ptr)
    {
        if (ptr == null) return null;
        int n = 0;
        while (ptr[n] != 0) n++;
        try
        {
            return Encoding.UTF8.GetString(ptr, n);
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct bleatcan_instance_id
    {
        public const int type_length = 32;
        public fixed byte type[type_length];
        public ulong timestamp;
        public uint process;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct bleatcan_instance_data
    {
        public byte* name, server;
    }

    static bleatcan_instance_id FromInstanceID(InstanceID i)
    {
        var o = new bleatcan_instance_id { timestamp = i.timestamp, process = i.process };
        FromString(i.type, o.type, bleatcan_instance_id.type_length);
        return o;
    }
    delegate void FromInstanceDelegate(bleatcan_instance_data* x);
    static void FromInstance(Instance i, FromInstanceDelegate action)
    {
        fixed (byte* name = FromString(i.name), server = FromString(i.server))
        {
            var o = new bleatcan_instance_data { name = name, server = server };
            action.Invoke(&o);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "bleatcan_instances_enumerate")]
    static uint bleatcan_instances_enumerate(delegate* unmanaged<nint, bleatcan_instance_id, bleatcan_instance_data*, uint> callback, nint userdata)
    {
        uint count = 0;
        try
        {
            foreach (var i in Instances.Enumerate())
            {
                if (callback != null)
                {
                    var id = FromInstanceID(i.id);
                    bool stop = false;
                    FromInstance(i, x =>
                    {
                        if (callback(userdata, id, x) == 0) stop = true;
                    });
                    if (stop) break;
                }
                count++;
            }
        }
        catch { }
        return count;
    }

    class InstancesReceiver : IInstancesReceiver
    {
        public nint handle, userdata;
        public delegate* unmanaged<nint, nint, bleatcan_instance_id, bleatcan_instance_data*, void> callback;
        public Instances instances;

        public void OnStart(Instance instance) => Callback(instance);
        public void OnChange(Instance instance) => Callback(instance);
        public void OnEnd(InstanceID id) => Callback(new Instance { id = id });

        void Callback(Instance instance)
        {
            var id = FromInstanceID(instance.id);
            FromInstance(instance, x => callback(handle, userdata, id, x));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "bleatcan_instances_listen")]
    static nint bleatcan_instances_listen(delegate* unmanaged<nint, nint, bleatcan_instance_id, bleatcan_instance_data*, void> callback, nint userdata)
    {
        if (callback == null) return 0;
        var i = new InstancesReceiver();
        i.handle = (nint)GCHandle.Alloc(i);
        i.userdata = userdata;
        i.callback = callback;
        i.instances = new Instances(i);
        return i.handle;
    }

    [UnmanagedCallersOnly(EntryPoint = "bleatcan_instances_unlisten")]
    static void bleatcan_instances_unlisten(nint handle)
    {
        if (handle == 0) return;
        var alloc = GCHandle.FromIntPtr(handle);
        if (alloc.IsAllocated && alloc.Target is InstancesReceiver i)
        {
            i.instances.Dispose();
            alloc.Free();
        }
    }

    class ConnectionReceiver : IConnectionReceiver
    {
        public nint handle, userdata;
        public delegate* unmanaged<nint, nint, bleatcan_connection_event*, void> callback;
        public Connection connection;

        public void OnConnect(Connection connection, bool active)
        {
            Callback(new bleatcan_connection_event
            {
                type = active ? bleatcan_connection_event.Type.CONNECTED : bleatcan_connection_event.Type.DISCONNECTED,
            });
        }
        public void OnError(Connection connection, ConnectionError error)
        {
            Callback(new bleatcan_connection_event
            {
                type = bleatcan_connection_event.Type.ERROR,
                error = error,
            });
        }
        public void OnReceive(Connection connection, string channel, ReadOnlySpan<byte> data)
        {
            byte[] channelU8;
            try
            {
                channelU8 = Encoding.UTF8.GetBytes(channel + '\0');
            }
            catch
            {
                channelU8 = null;
            }
            if (channelU8 == null || channelU8.Length <= 1) return;
            byte nothing = 0;
            int n = data.Length;
            if (n == 0) data = new Span<byte>(&nothing, 1);
            fixed (byte* c = channelU8, d = data)
            {
                Callback(new bleatcan_connection_event
                {
                    type = bleatcan_connection_event.Type.RECEIVE,
                    channel = c,
                    data = d,
                    length = n,
                });
            }
        }

        void Callback(bleatcan_connection_event ev) => callback(handle, userdata, &ev);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct bleatcan_connection_event
    {
        public enum Type : uint
        {
            CONNECTED,
            DISCONNECTED,
            RECEIVE,
            ERROR,
        }
        public Type type;
        public ConnectionError error;
        public byte* channel, data;
        public int length;
    }


    [UnmanagedCallersOnly(EntryPoint = "bleatcan_connection_create")]
    static nint bleatcan_connection_create(byte* server, byte* name, delegate* unmanaged<nint, nint, bleatcan_connection_event*, void> callback, nint userdata)
    {
        if (callback == null) return 0;
        var i = new ConnectionReceiver();
        i.handle = (nint)GCHandle.Alloc(i);
        i.userdata = userdata;
        i.callback = callback;
        i.connection = new Connection(ToString(server), ToString(name), i);
        return i.handle;
    }

    [UnmanagedCallersOnly(EntryPoint = "bleatcan_connection_destroy")]
    static void bleatcan_connection_destroy(nint handle)
    {
        if (handle == 0) return;
        var alloc = GCHandle.FromIntPtr(handle);
        if (alloc.IsAllocated && alloc.Target is ConnectionReceiver i)
        {
            i.connection.Dispose();
            alloc.Free();
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "bleatcan_connection_send")]
    static bool bleatcan_connection_send(nint handle, byte* channel, byte* data, int length)
    {
        if (handle == 0 || channel == null || data != null && length < 0) return false;
        var channelString = ToString(channel);
        if (string.IsNullOrEmpty(channelString)) return false;
        var alloc = GCHandle.FromIntPtr(handle);
        if (!alloc.IsAllocated || alloc.Target is not ConnectionReceiver i) return false;
        return i.connection.Send(channelString, data != null && length > 0 ? new Span<byte>(data, length) : default);
    }
}
