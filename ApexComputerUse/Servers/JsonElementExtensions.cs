using System.Text.Json;

namespace ApexComputerUse
{
    internal static class JsonElementExtensions
    {
        public static string? Str(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetRawText(),
                _                   => null
            };
        }

        public static int? Int(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int v) ? v : null;
        }

        public static float? Float(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind == JsonValueKind.Number ? (float?)p.GetSingle() : null;
        }

        public static bool? Bool(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind == JsonValueKind.True  ? true
                 : p.ValueKind == JsonValueKind.False ? false
                 : null;
        }
    }
}
