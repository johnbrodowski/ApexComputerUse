namespace ApexComputerUse
{
    // -- Canonical result type ---------------------------------------------

    public sealed class ApexResult
    {
        public bool                        Success   { get; init; }
        public string                      Action    { get; init; } = "";
        public Dictionary<string, string>? Data      { get; init; }
        public string?                     Error     { get; init; }
        public Dictionary<string, object>? ErrorData { get; init; }

        /// <summary>Adapt a legacy CommandResponse into the canonical form.</summary>
        public static ApexResult From(string action, CommandResponse cr)
        {
            Dictionary<string, string>? data = null;
            if (!string.IsNullOrEmpty(cr.Data))
                data = new Dictionary<string, string> { ["result"] = cr.Data };
            if (!string.IsNullOrEmpty(cr.Message))
            {
                data ??= new Dictionary<string, string>();
                data["message"] = cr.Message;
            }
            // Merge handler-supplied extras (e.g. gettext source). Extras win on key collision
            // so handlers can deliberately override a default field.
            if (cr.Extras != null)
            {
                data ??= new Dictionary<string, string>();
                foreach (var kv in cr.Extras) data[kv.Key] = kv.Value;
            }
            return new ApexResult
            {
                Success   = cr.Success,
                Action    = action,
                Data      = data,
                Error     = cr.Success ? null : cr.Message,
                ErrorData = cr.ErrorData
            };
        }
    }
}

