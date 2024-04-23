using System.Globalization;

namespace VeadoTube.BleatCan;

public readonly struct InstanceID : IEquatable<InstanceID>, IComparable<InstanceID>
{
    public readonly string type;
    public readonly ulong timestamp;
    public readonly uint process;

    public readonly bool isValid => !string.IsNullOrEmpty(type);
    public override readonly string ToString() => isValid ? $"{type}-{timestamp:x16}-{process:x8}" : "";

    public readonly bool Equals(InstanceID i) => isValid ? i.isValid && type == i.type && timestamp == i.timestamp && process == i.process : !i.isValid;
    public override readonly bool Equals(object o) => o is InstanceID i && Equals(i);
    public override readonly int GetHashCode() => isValid ? HashCode.Combine(type, timestamp, process) : 0;
    public int CompareTo(InstanceID i)
    {
        int c = timestamp.CompareTo(i.timestamp);
        return c != 0 ? c : ToString().CompareTo(i.ToString());
    }
    public static bool operator ==(InstanceID a, InstanceID b) => a.Equals(b);
    public static bool operator !=(InstanceID a, InstanceID b) => !a.Equals(b);

    public InstanceID(string s)
    {
        type = null;
        timestamp = 0;
        process = 0;
        if (string.IsNullOrEmpty(s)) return;
        var span = s.AsSpan();
        int i = span.IndexOf('-');
        if (i <= 0) return;
        var typeSpan = span.Slice(0, i);
        if (!Validate(typeSpan)) return;
        span = span.Slice(i + 1);
        if (span.Length != 25 || span[16] != '-') return;
        var timestampSpan = span.Slice(0, 16);
        var processSpan = span.Slice(17, 8);
        if (!Validate(timestampSpan) || !ulong.TryParse(timestampSpan, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out timestamp)) return;
        if (!Validate(processSpan) || !uint.TryParse(processSpan, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out process)) return;
        type = typeSpan.ToString();
        static bool Validate(ReadOnlySpan<char> s)
        {
            foreach (var i in s)
            {
                if (i >= '0' && i <= '9' || i >= 'a' && i <= 'z') continue;
                return false;
            }
            return true;
        }
    }
}
