using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Maps a JSON object into a <see cref="CommandRequest"/>. Used by both the HTTP server
    /// (POST bodies) and the named-pipe server (newline-delimited JSON lines) - those two
    /// hand-rolled the same field mapping with subtly different subsets before. Adding a new
    /// <see cref="CommandRequest"/> property now means exactly one edit here, not two.
    /// </summary>
    public static class CommandRequestJsonMapper
    {
        /// <summary>Parses <paramref name="json"/> into a CommandRequest with the given
        /// <paramref name="command"/> verb and optional default <paramref name="action"/>.
        /// Malformed JSON returns a request with just the command filled in - callers decide
        /// whether to fail hard or forward it to the processor for an "ERR" response.</summary>
        public static CommandRequest FromJson(string? json, string command, string? action = null)
        {
            var r = new CommandRequest { Command = command, Action = action };
            if (string.IsNullOrWhiteSpace(json)) return r;
            try
            {
                using var doc = JsonDocument.Parse(json);
                Populate(r, doc.RootElement);
            }
            catch (Exception ex) { AppLog.Debug($"[JsonMapper] Malformed JSON - {ex.Message}"); }
            return r;
        }

        /// <summary>Field-level populator used by the line-oriented pipe protocol, where the
        /// command verb lives inside the JSON object rather than coming from a URL route.</summary>
        public static CommandRequest FromJsonSelfDescribing(string json)
        {
            // Empty or malformed input maps to "help" - the pipe client gets a useful
            // response rather than an "Unknown command ''" error. Valid JSON that
            // simply omits "command" leaves it blank; the processor surfaces that.
            if (string.IsNullOrWhiteSpace(json))
                return new CommandRequest { Command = "help" };

            var r = new CommandRequest();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                r.Command = root.Str("command") ?? "";
                Populate(r, root);
            }
            catch (Exception ex)
            {
                AppLog.Debug($"[JsonMapper] Malformed JSON - {ex.Message}");
                r.Command = "help";
            }
            return r;
        }

        private static void Populate(CommandRequest r, JsonElement root)
        {
            r.Window       = root.Str("window")       ?? r.Window;
            r.AutomationId = root.Str("automationId") ?? root.Str("id") ?? root.Str("element") ?? r.AutomationId;
            r.ElementName  = root.Str("elementName")  ?? root.Str("name") ?? r.ElementName;
            r.SearchType   = root.Str("searchType")   ?? root.Str("type") ?? r.SearchType;
            r.Action       = root.Str("action")       ?? r.Action;
            r.Value        = root.Str("value")        ?? r.Value;
            r.ModelPath    = root.Str("model")        ?? root.Str("modelPath") ?? r.ModelPath;
            r.MmProjPath   = root.Str("proj")         ?? root.Str("mmProjPath") ?? r.MmProjPath;
            r.Prompt       = root.Str("prompt")       ?? r.Prompt;
            r.Match        = root.Str("match")        ?? r.Match;
            r.Properties   = root.Str("properties")   ?? r.Properties;
            r.ChangedSince = root.Str("changedSince") ?? root.Str("since") ?? r.ChangedSince;

            if (root.TryGetProperty("depth", out var dEl) &&
                dEl.ValueKind == JsonValueKind.Number &&
                dEl.TryGetInt32(out int dVal))
                r.Depth = dVal;

            r.OnscreenOnly   = root.Bool("onscreen")       ?? r.OnscreenOnly;
            r.CollapseChains = root.Bool("collapseChains") ?? r.CollapseChains;
            r.IncludePath    = root.Bool("includePath")    ?? r.IncludePath;
        }
    }
}

