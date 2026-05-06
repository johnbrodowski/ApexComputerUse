using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    /// <summary>
    /// One rectangular region the monitor watches. Multiple per monitor — e.g. an LED bank
    /// or a row of indicator icons — each diffed independently. Coordinates are absolute
    /// screen pixels.
    /// </summary>
    public sealed class MonitorRegion
    {
        [JsonPropertyName("x")]      public int X      { get; set; }
        [JsonPropertyName("y")]      public int Y      { get; set; }
        [JsonPropertyName("width")]  public int Width  { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        /// Optional human-readable name surfaced in fire events ("power-led", "alert-icon").
        [JsonPropertyName("label")]  public string? Label { get; set; }
    }

    /// <summary>
    /// Persistent screen-region change monitor. Fires SSE events on the /events stream
    /// when any region's diff exceeds the threshold. Designed for cases UIA can't see —
    /// canvas-rendered indicators, custom-drawn lights, video frames, hardware status icons.
    ///
    /// The monitor as a whole represents one logical "watch" (e.g. all the indicators on
    /// one dashboard); each region is one indicator within it. Fires are per-region.
    /// </summary>
    public sealed class RegionMonitor
    {
        [JsonPropertyName("id")]        public string Id       { get; set; } = SceneIds.New();
        [JsonPropertyName("name")]      public string Name     { get; set; } = "Untitled";

        [JsonPropertyName("regions")]   public List<MonitorRegion> Regions { get; set; } = new();

        /// Polling cadence in milliseconds. Floor enforced at 100ms by the runner.
        [JsonPropertyName("intervalMs")]   public int    IntervalMs   { get; set; } = 1000;
        /// Percent of pixels that must change for a fire (0–100).
        [JsonPropertyName("thresholdPct")] public double ThresholdPct { get; set; } = 5.0;
        /// Per-channel max-diff under which a pixel is considered unchanged (0–255).
        /// Defaults to 8 — ignores antialias jitter and minor compression noise.
        [JsonPropertyName("tolerance")]    public int    Tolerance    { get; set; } = 8;

        /// When false, the runner skips this monitor. AI calls /monitor/{id}/start to enable.
        [JsonPropertyName("enabled")]      public bool   Enabled      { get; set; } = false;

        // -- Last-fire telemetry, surfaced via GET /monitor/{id} --
        [JsonPropertyName("lastFiredUtc")]    public DateTime? LastFiredUtc    { get; set; }
        [JsonPropertyName("lastPercentDiff")] public double?   LastPercentDiff { get; set; }
        [JsonPropertyName("lastRegionIndex")] public int?      LastRegionIndex { get; set; }
        [JsonPropertyName("hitCount")]        public long      HitCount        { get; set; }

        [JsonPropertyName("notes")]         public string?  Notes        { get; set; }
        [JsonPropertyName("createdUtc")]    public DateTime CreatedUtc   { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("updatedUtc")]    public DateTime UpdatedUtc   { get; set; } = DateTime.UtcNow;

        public void Touch() => UpdatedUtc = DateTime.UtcNow;
    }
}
