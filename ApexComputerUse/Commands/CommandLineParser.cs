using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexComputerUse
{
    /// <summary>
    /// Single source of truth for the "key=value key2=\"multi word\"" text command format used
    /// by the Form1 GUI console tab, the Telegram controller, and the Windows named-pipe server.
    /// Each of those sources used to reimplement this verbatim \- if you need to add a new
    /// command verb or flag, add it here once.
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>Splits "window=Notepad name=\"File menu\" type=Button" into a dictionary.</summary>
        public static Dictionary<string, string> Tokenize(string input)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < input.Length)
            {
                while (i < input.Length && input[i] == ' ') i++;
                if (i >= input.Length) break;

                int keyStart = i;
                while (i < input.Length && input[i] != '=' && input[i] != ' ') i++;
                string key = input[keyStart..i].Trim();
                if (string.IsNullOrEmpty(key)) { i++; continue; }

                if (i >= input.Length || input[i] != '=') { result[key] = ""; continue; }
                i++; // skip '='

                string value;
                if (i < input.Length && input[i] == '"')
                {
                    i++;
                    int vs = i;
                    while (i < input.Length && input[i] != '"') i++;
                    value = input[vs..i];
                    if (i < input.Length) i++;
                }
                else
                {
                    int vs = i;
                    while (i < input.Length && input[i] != ' ') i++;
                    value = input[vs..i];
                }

                result[key] = value;
            }
            return result;
        }

        /// <summary>
        /// Parses a whole command line ("find window=Notepad name=File") into a CommandRequest.
        /// Returns null if the verb is unknown \- callers decide whether unknown should be an
        /// error (GUI) or a passthrough (Telegram <c>/foo bar</c> \-> Command=foo).
        /// </summary>
        public static CommandRequest? Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd  = parts[0].ToLowerInvariant();
            string args = parts.Length > 1 ? parts[1] : "";
            return Build(cmd, args);
        }

        /// <summary>Pre-split variant \- used by Telegram after stripping "/" and the optional @botname.</summary>
        public static CommandRequest? Build(string cmd, string args)
        {
            cmd = cmd.ToLowerInvariant();
            var kv = Tokenize(args);

            return cmd switch
            {
                "find" => new CommandRequest
                {
                    Command      = "find",
                    Window       = kv.Get("window", "w"),
                    AutomationId = kv.Get("id", "automationid"),
                    ElementName  = kv.Get("name", "n"),
                    SearchType   = kv.Get("type", "t")
                },
                "execute" or "exec" => new CommandRequest
                {
                    Command = "execute",
                    Action  = kv.Get("action", "a"),
                    Value   = kv.Get("value", "v")
                },
                "ocr" => new CommandRequest
                {
                    Command = "ocr",
                    Value   = kv.Get("value", "region") ?? (args.Contains(',') ? args : null)
                },
                "capture" => new CommandRequest
                {
                    Command = "capture",
                    Action  = kv.Get("action", "a"),
                    Value   = kv.Get("value", "v")
                },
                "status"   => new CommandRequest { Command = "status" },
                "windows"  => new CommandRequest { Command = "windows" },
                "elements" => new CommandRequest
                {
                    Command      = "elements",
                    SearchType   = kv.Get("type", "t") ?? (args.Length > 0 ? args.Trim() : null),
                    ChangedSince = kv.Get("since", "changedsince", "hash")
                },
                "ai" => new CommandRequest
                {
                    Command    = "ai",
                    Action     = kv.Get("action", "a")
                                 ?? args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault()?.ToLowerInvariant(),
                    ModelPath  = kv.Get("model"),
                    MmProjPath = kv.Get("proj"),
                    Value      = kv.Get("value", "path", "v"),
                    Prompt     = kv.Get("prompt", "p")
                },
                "help" or "start" => new CommandRequest { Command = "help" },
                _ => null
            };
        }
    }

    public static class CommandArgDictExtensions
    {
        /// Returns the value for the first key present with non-whitespace content.
        public static string? Get(this Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }
    }
}
