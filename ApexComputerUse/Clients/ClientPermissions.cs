using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    public sealed class ClientPermissions
    {
        /// <summary>find, exec, elements, windows, status, uimap</summary>
        [JsonPropertyName("allow_automation")] public bool AllowAutomation { get; set; } = true;

        /// <summary>capture, ocr</summary>
        [JsonPropertyName("allow_capture")]    public bool AllowCapture    { get; set; } = true;

        /// <summary>ai/*, chat/*</summary>
        [JsonPropertyName("allow_ai")]         public bool AllowAi         { get; set; } = true;

        /// <summary>scenes/*, editor</summary>
        [JsonPropertyName("allow_scenes")]     public bool AllowScenes     { get; set; } = true;

        /// <summary>/run shell execution — off by default (dangerous)</summary>
        [JsonPropertyName("allow_shell_run")]  public bool AllowShellRun   { get; set; } = false;

        /// <summary>access to client list and cross-client control — off by default (isolation)</summary>
        [JsonPropertyName("allow_clients")]    public bool AllowClients    { get; set; } = false;

        /// <summary>ping, metrics, sysinfo, env, ls — on by default for all registered clients</summary>
        [JsonPropertyName("allow_diagnostics")] public bool AllowDiagnostics { get; set; } = true;
    }
}
