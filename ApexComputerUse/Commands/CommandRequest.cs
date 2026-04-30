namespace ApexComputerUse
{
    public class CommandRequest
    {
        public string  Command      { get; set; } = "";
        public string? Window       { get; set; }
        public string? AutomationId { get; set; }
        public string? ElementName  { get; set; }
        public string? SearchType   { get; set; }   // "All" or a ControlType name
        public bool    OnscreenOnly { get; set; }   // true = exclude IsOffscreen elements
        public string? Action       { get; set; }
        public string? Value        { get; set; }
        public string? ModelPath    { get; set; }   // ai init — LLM model .gguf path
        public string? MmProjPath   { get; set; }   // ai init — mmproj .gguf path
        public string? Prompt       { get; set; }   // ai describe/ask — question text
        public int?    Depth        { get; set; }   // elements — max tree depth (null = unlimited)

        // ── Browser-friendly tree shaping (opt-in; all default to inert) ──

        public string? Match          { get; set; }

        public bool    CollapseChains { get; set; }

        public bool    IncludePath    { get; set; }

        public string? Properties     { get; set; }

        public string? ChangedSince   { get; set; }
    }

    public class CommandResponse
    {
        public bool    Success { get; set; }
        public string  Message { get; set; } = "";
        public string? Data    { get; set; }

        public string ToText() =>
            Data != null
                ? $"{(Success ? "OK" : "ERR")} {Message}\n{Data}"
                : $"{(Success ? "OK" : "ERR")} {Message}";

        public string ToJson()
        {
            var obj = new { success = Success, message = Message, data = Data };
            return System.Text.Json.JsonSerializer.Serialize(obj, FormatAdapter.s_indented);
        }
    }
}
