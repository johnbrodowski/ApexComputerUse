using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // JSON parsing 

        // HttpListenerRequest.QueryString decodes '+' as space (form-encoding), which breaks
        // key combo strings like "Ctrl+A". Parse 'value' from the raw URL to preserve '+'.
        private static string? RawQueryValue(HttpListenerRequest req)
        {
            var raw = req.Url?.Query;
            if (raw == null) return null;
            foreach (var part in raw.TrimStart('?').Split('&'))
            {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                if (Uri.UnescapeDataString(part[..eq]) == "value")
                    return Uri.UnescapeDataString(part[(eq + 1)..]);
            }
            return null;
        }

        private static CommandRequest FromQueryString(HttpListenerRequest req, string command, string? action = null)
            => new()
            {
                Command      = command,
                Action       = req.QueryString["action"]       ?? action,
                Value        = RawQueryValue(req),
                Window       = req.QueryString["window"],
                AutomationId = req.QueryString["id"]           ?? req.QueryString["automationId"]
                             ?? req.QueryString["element"],
                ElementName  = req.QueryString["name"]         ?? req.QueryString["elementName"],
                SearchType   = req.QueryString["type"]         ?? req.QueryString["searchType"],
                OnscreenOnly = string.Equals(req.QueryString["onscreen"], "true",
                                   StringComparison.OrdinalIgnoreCase),
                Prompt       = req.QueryString["prompt"],
                ModelPath    = req.QueryString["model"]        ?? req.QueryString["modelPath"],
                MmProjPath   = req.QueryString["proj"]         ?? req.QueryString["mmProjPath"],
                Depth          = int.TryParse(req.QueryString["depth"], out int _d) ? _d : null,
                Match          = req.QueryString["match"],
                CollapseChains = string.Equals(req.QueryString["collapseChains"], "true", StringComparison.OrdinalIgnoreCase),
                IncludePath    = string.Equals(req.QueryString["includePath"],    "true", StringComparison.OrdinalIgnoreCase),
                Properties     = req.QueryString["properties"],
                Property       = req.QueryString["property"],
                Predicate      = req.QueryString["predicate"],
                Expected       = req.QueryString["expected"],
                Timeout        = int.TryParse(req.QueryString["timeout"],  out int _t) ? _t : null,
                Interval       = int.TryParse(req.QueryString["interval"], out int _i) ? _i : null,
            };

        private static CommandRequest FromJson(string json, string command, string? action = null)
            => CommandRequestJsonMapper.FromJson(json, command, action);

        internal static string? ParseJsonString(string json, string field)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty(field, out var el) ? el.GetString() : null;
            }
            catch { return null; }
        }

    }
}
