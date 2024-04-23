using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VeadoTube.BleatCan;

public static class JsonUtils
{
    public static bool ReadObject(ReadOnlySpan<byte> data, out JsonObject obj)
    {
        var reader = new Utf8JsonReader(data);
        try
        {
            obj = JsonNode.Parse(ref reader) as JsonObject;
        }
        catch
        {
            obj = null;
        }
        return obj != null;
    }
    public static byte[] AsUTF8(this JsonNode node)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) node.WriteTo(w);
        return ms.ToArray();
    }

    public static JsonArray ToJsonArray<T>(this IEnumerable<T> source, Func<T, JsonNode> selector)
    {
        return new JsonArray(source.Select(selector).ToArray());
    }
    public static JsonObject ToJsonObject<T>(this IEnumerable<T> source, Func<T, string> keySelector, Func<T, JsonNode> valueSelector)
    {
        return new JsonObject(source.Select(x => new KeyValuePair<string, JsonNode>(keySelector(x), valueSelector(x))));
    }

    public static bool TryGetValue2<T>(this JsonValue value, out T result)
    {
        if (value.TryGetValue(out result)) return true;
        result = default;
        if (!value.TryGetValue(out string s)) return false;
        switch (default(T))
        {
            case float:
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f is T fx)
                {
                    result = fx;
                    return true;
                }
                return false;
            case double:
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d is T dx)
                {
                    result = dx;
                    return true;
                }
                return false;
            case int:
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i is T ix)
                {
                    result = ix;
                    return true;
                }
                return false;
        }
        return false;
    }

    public static bool TryGetProperty<T>(this JsonObject obj, string key, out T result)
    {
        result = default;
        return obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue2(out result);
    }
    public static T GetProperty<T>(this JsonObject obj, string key) => TryGetProperty<T>(obj, key, out var result) ? result : default;

    public static bool TryGetPropertyArray(this JsonObject obj, string key, out JsonArray result)
    {
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonArray x)
        {
            result = x;
            return true;
        }
        result = null;
        return false;
    }

    public static bool TryGetPropertyObject(this JsonObject obj, string key, out JsonObject result)
    {
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonObject x)
        {
            result = x;
            return true;
        }
        result = null;
        return false;
    }

    public static bool TryGetElement<T>(this JsonArray array, int index, out T result)
    {
        result = default;
        return index >= 0 && index < array.Count && array[index] is JsonValue value && value.TryGetValue2(out result);
    }
    public static T GetElement<T>(this JsonArray array, int index) => TryGetElement<T>(array, index, out var result) ? result : default;

    public static bool TryGetElementArray(this JsonArray array, int index, out JsonArray result)
    {
        if (index >= 0 && index < array.Count && array[index] is JsonArray x)
        {
            result = x;
            return true;
        }
        result = null;
        return false;
    }

    public static bool TryGetElementObject(this JsonArray array, int index, out JsonObject result)
    {
        if (index >= 0 && index < array.Count && array[index] is JsonObject x)
        {
            result = x;
            return true;
        }
        result = null;
        return false;
    }
}
