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
        public string? ModelPath    { get; set; }   // ai init - LLM model .gguf path
        public string? MmProjPath   { get; set; }   // ai init - mmproj .gguf path
        public string? Prompt       { get; set; }   // ai describe/ask - question text
        public int?    Depth        { get; set; }   // elements - max tree depth (null = unlimited)

        // -- Browser-friendly tree shaping (opt-in; all default to inert) --

        public string? Match          { get; set; }

        public bool    CollapseChains { get; set; }

        public bool    IncludePath    { get; set; }

        public string? Properties     { get; set; }

        public string? ChangedSince   { get; set; }

        // -- waitfor exec action (action=waitfor) --------------------------
        // Predicate-based polling: wait until <Property> on the current element <Predicate> <Expected>.
        // E.g. property=value predicate=contains expected="built successfully" timeout=15000.
        public string? Property        { get; set; }   // value | text | name | isvisible | isenabled
        public string? Predicate       { get; set; }   // equals | contains | not-empty | visible | gone
        public string? Expected        { get; set; }   // expected value (used by equals/contains)
        public int?    Timeout         { get; set; }   // total timeout in ms (default 10000)
        public int?    Interval        { get; set; }   // poll interval in ms (default 200)

        // -- Batch mode (/exec) --------------------------------------------
        // When Actions is non-null, /exec runs each entry as a full sub-request. Each entry is
        // a CommandRequest (recursive shape); its `cmd` field defaults to "execute" so simple
        // batches read as just [{action:"type",value:"hello"},{action:"click"}]. Mid-batch
        // find/capture/ocr work because each entry can specify any cmd.
        public List<CommandRequest>? Actions { get; set; }
        public bool? StopOnError { get; set; }   // default true: first failure stops the batch

        // Set by JSON mappers when the body fails to parse - lets the dispatcher emit
        // a clean "Invalid JSON: ..." error instead of letting blank fields surface as
        // misleading downstream messages like "'action' is required".
        public string? JsonParseError { get; set; }
    }

    public class CommandResponse
    {
        public bool    Success { get; set; }
        public string  Message { get; set; } = "";
        public string? Data    { get; set; }

        // Optional caller-facing advisory string. Use this for soft signals like graceful-fallback
        // notes ("ran on parent ancestor") or hints that a 'value' field was ignored. Surfaces as
        // ApexResult.Data["warning"] so machine readers can branch on it without parsing free text
        // out of Data. Null when no advisory applies. Distinct from ErrorData (hard failure detail).
        public string? Warning { get; set; }

        // Optional structured side-channel for handlers that want to surface extra fields
        // alongside Data (e.g. gettext returning the source UIA pattern). Merged into the
        // final ApexResult.Data dictionary by ApexResult.From. Null when nothing was attached.
        public Dictionary<string, string>? Extras { get; set; }

        // Optional structured error payload (e.g. supported_patterns + element_state when an
        // action fails because the element doesn't support the required pattern). Surfaces as
        // ApexResult.ErrorData -> response field "error_data". Null on success or when an
        // error has no structured detail.
        public Dictionary<string, object>? ErrorData { get; set; }

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

