using System.Net;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    // -- Format adapter ----------------------------------------------------

    internal static class FormatAdapter
    {
        internal static readonly JsonSerializerOptions s_indented = new()
        {
            WriteIndented = true,
            Encoder       = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal static readonly JsonSerializerOptions s_indentedCamel = new()
        {
            WriteIndented          = true,
            PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Encoder                = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal static readonly JsonSerializerOptions s_compact = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string Negotiate(HttpListenerRequest req, string? extHint = null)
        {
            // 1. URL file extension takes highest priority
            if (extHint is "pdf")           return "pdf";
            if (extHint is "json")          return "json";
            if (extHint is "html" or "htm") return "html";
            if (extHint is "txt" or "text") return "text";

            // 2. ?format= query parameter
            string? qf = req.QueryString["format"]?.ToLowerInvariant();
            if (qf is "json" or "html" or "text" or "pdf") return qf;

            // 3. Accept header
            string accept = req.Headers["Accept"] ?? "";
            if (accept.Contains("application/pdf",  StringComparison.OrdinalIgnoreCase)) return "pdf";
            if (accept.Contains("text/html",        StringComparison.OrdinalIgnoreCase)) return "html";
            if (accept.Contains("text/plain",       StringComparison.OrdinalIgnoreCase)) return "text";
            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return "json";
            return "html"; // default
        }

        public static (byte[] body, string contentType, int statusCode) Render(ApexResult r, string format)
            => format switch
            {
                "json" => AsUtf8(RenderJson(r), "application/json; charset=utf-8", r.Success),
                "text" => AsUtf8(RenderText(r), "text/plain; charset=utf-8",       r.Success),
                "pdf"  => RenderPdf(r),
                _      => AsUtf8(RenderHtml(r), "text/html; charset=utf-8",        r.Success),
            };

        private static (byte[], string, int) AsUtf8(string body, string ct, bool ok)
            => (Encoding.UTF8.GetBytes(body), ct, ok ? 200 : 400);

        private static string RenderJson(ApexResult r) =>
            JsonSerializer.Serialize(
                new { success = r.Success, action = r.Action, data = r.Data, error = r.Error },
                s_indented);

        private static string RenderText(ApexResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"success: {r.Success}");
            sb.AppendLine($"action:  {r.Action}");
            if (r.Error is not null) sb.AppendLine($"error:   {r.Error}");
            if (r.Data  is not null)
                foreach (var kv in r.Data)
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
            return sb.ToString();
        }

        private static string RenderHtml(ApexResult r)
        {
            string embeddedJson = JsonSerializer.Serialize(
                new { success = r.Success, action = r.Action, data = r.Data, error = r.Error },
                new JsonSerializerOptions { WriteIndented = true });
            // Prevent </script> injection in embedded JSON
            embeddedJson = embeddedJson.Replace("</", @"<\/", StringComparison.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine($"success: {r.Success}");
            sb.AppendLine($"action:  {r.Action}");
            if (r.Error is not null) sb.AppendLine($"error:   {r.Error}");
            if (r.Data  is not null)
                foreach (var kv in r.Data)
                    sb.AppendLine($"{kv.Key}: {WebUtility.HtmlEncode(kv.Value)}");

            string title  = WebUtility.HtmlEncode(r.Action);
            string color  = r.Success ? "#4ec94e" : "#e05252";
            string preTxt = WebUtility.HtmlEncode(sb.ToString());

            string html = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head><meta charset="utf-8"><title>{{title}}</title>
                <style>body{font-family:monospace;padding:1em;background:#1e1e1e;color:#d4d4d4}
                h2{color:{{color}}}pre{background:#252526;padding:1em;border-radius:4px;white-space:pre-wrap}</style>
                </head>
                <body>
                <h2>{{title}}</h2>
                <pre>{{preTxt}}</pre>
                <script type="application/json" id="apex-result">
                {{embeddedJson}}
                </script>
                </body></html>
                """;
            return html;
        }

        private static (byte[], string, int) RenderPdf(ApexResult r)
        {
            var lines = new List<string>();
            lines.Add($"Apex  {(r.Success ? "OK" : "ERR")}  {r.Action}");
            lines.Add(new string('-', 64));
            if (r.Error is not null) lines.Add($"error: {r.Error}");
            if (r.Data  is not null)
                foreach (var kv in r.Data)
                {
                    string line = $"{kv.Key}: {kv.Value}";
                    while (line.Length > 90)
                    {
                        lines.Add(line[..90]);
                        line = "  " + line[90..];
                    }
                    lines.Add(line);
                }
            byte[] pdf = PdfWriter.GenerateTextPdf(lines);
            return (pdf, "application/pdf", r.Success ? 200 : 400);
        }
    }

    // -- Minimal raw PDF generator (no external dependencies) -----------------

    internal static class PdfWriter
    {
        public static byte[] GenerateTextPdf(List<string> lines)
        {
            const float W  = 595f, H = 842f, M = 50f; // A4, margins
            const float Sz = 9f,   Lh = 12f;
            int lpp = (int)((H - 2 * M) / Lh);        // lines per page

            // Split into pages (at least one, even if empty)
            var pages = new List<List<string>>();
            if (lines.Count == 0)
            {
                pages.Add([]);
            }
            else
            {
                for (int i = 0; i < lines.Count; i += lpp)
                    pages.Add(lines.Skip(i).Take(lpp).ToList());
            }

            // Object IDs: 1=Catalog 2=Pages 3=Font 4..=Page objs (4+n)..=Content streams
            int nPages      = pages.Count;
            int firstPage   = 4;
            int firstStream = firstPage + nPages;

            // Build in Latin-1: 1 char == 1 byte -> sb.Length gives exact byte offsets
            var sb      = new StringBuilder();
            var offsets = new List<int>();

            sb.Append("%PDF-1.4\n");

            // obj 1 - Catalog
            offsets.Add(sb.Length);
            sb.Append("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");

            // obj 2 - Pages
            string kids = string.Join(" ", Enumerable.Range(firstPage, nPages).Select(i => $"{i} 0 R"));
            offsets.Add(sb.Length);
            sb.Append($"2 0 obj<</Type/Pages/Kids[{kids}]/Count {nPages}>>endobj\n");

            // obj 3 - Font (Courier built-in - no embedding needed)
            offsets.Add(sb.Length);
            sb.Append("3 0 obj<</Type/Font/Subtype/Type1/BaseFont/Courier/Encoding/WinAnsiEncoding>>endobj\n");

            // Page objects
            for (int i = 0; i < nPages; i++)
            {
                offsets.Add(sb.Length);
                sb.Append($"{firstPage + i} 0 obj<</Type/Page/Parent 2 0 R" +
                          $"/MediaBox[0 0 {W} {H}]" +
                          $"/Contents {firstStream + i} 0 R" +
                          $"/Resources<</Font<</F1 3 0 R>>>>>>endobj\n");
            }

            // Content streams
            for (int i = 0; i < nPages; i++)
            {
                var cs = new StringBuilder();
                cs.Append($"BT /F1 {Sz} Tf {M} {H - M - Sz} Td {Lh} TL\n");
                foreach (var line in pages[i])
                    cs.Append($"({PdfEscapeString(line)}) Tj T*\n");
                cs.Append("ET\n");
                string stream = cs.ToString();
                int    len    = stream.Length; // Latin-1: chars == bytes

                offsets.Add(sb.Length);
                sb.Append($"{firstStream + i} 0 obj<</Length {len}>>stream\n");
                sb.Append(stream);
                sb.Append("endstream endobj\n");
            }

            // xref table
            int xrefPos   = sb.Length;
            int totalObjs = 3 + nPages + nPages; // catalog + pages + font + page objs + streams
            sb.Append($"xref\n0 {totalObjs + 1}\n");
            sb.Append("0000000000 65535 f \n");
            foreach (var off in offsets)
                sb.Append($"{off:D10} 00000 n \n");

            sb.Append($"trailer<</Size {totalObjs + 1}/Root 1 0 R>>\n");
            sb.Append($"startxref\n{xrefPos}\n%%EOF");

            return Encoding.Latin1.GetBytes(sb.ToString());
        }

        private static string PdfEscapeString(string s)
        {
            var sb = new StringBuilder(s.Length + 4);
            foreach (char c in s)
            {
                if (c == '(' || c == ')' || c == '\\') sb.Append('\\');
                // Replace non-ASCII / non-printable with a space
                sb.Append(c is >= ' ' and < (char)127 ? c : ' ');
            }
            return sb.ToString();
        }
    }
}

