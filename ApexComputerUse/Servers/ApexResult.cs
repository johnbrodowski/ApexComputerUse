namespace ApexComputerUse
{
    // ── Canonical result type ─────────────────────────────────────────────

    public sealed class ApexResult
    {
        public bool                        Success { get; init; }
        public string                      Action  { get; init; } = "";
        public Dictionary<string, string>? Data    { get; init; }
        public string?                     Error   { get; init; }

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
            return new ApexResult
            {
                Success = cr.Success,
                Action  = action,
                Data    = data,
                Error   = cr.Success ? null : cr.Message
            };
        }
    }
}
