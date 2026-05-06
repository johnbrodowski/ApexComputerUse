using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    /// <summary>
    /// Persistent named pixel-coordinate grid tied to a window or stable element-hash.
    /// Built for AI self-calibration loops: overlay grid → capture → adjust → store,
    /// then re-use saved cell coordinates next session via <c>click-at</c>.
    ///
    /// Coordinates are absolute screen pixels (matching what /capture screen returns).
    /// </summary>
    public sealed class RegionMap
    {
        [JsonPropertyName("id")]            public string  Id            { get; set; } = SceneIds.New();
        [JsonPropertyName("name")]          public string  Name          { get; set; } = "Untitled";

        // -- Scope (one or both may be set; both null = global / desktop) --
        [JsonPropertyName("windowTitle")]   public string? WindowTitle   { get; set; }
        [JsonPropertyName("elementHash")]   public string? ElementHash   { get; set; }

        // -- Grid geometry (origin in screen pixels) ----------------------
        [JsonPropertyName("originX")]       public int     OriginX       { get; set; }
        [JsonPropertyName("originY")]       public int     OriginY       { get; set; }
        [JsonPropertyName("cellWidth")]     public int     CellWidth     { get; set; } = 64;
        [JsonPropertyName("cellHeight")]    public int     CellHeight    { get; set; } = 64;
        [JsonPropertyName("rows")]          public int     Rows          { get; set; } = 8;
        [JsonPropertyName("cols")]          public int     Cols          { get; set; } = 8;

        // -- Display ------------------------------------------------------
        [JsonPropertyName("color")]         public string  Color         { get; set; } = "#33FF33";
        /// Optional row-major label per cell (Labels[row][col]). Sparse OK; null for unset cells.
        [JsonPropertyName("labels")]        public string?[][]? Labels   { get; set; }
        [JsonPropertyName("notes")]         public string? Notes         { get; set; }

        [JsonPropertyName("createdUtc")]    public DateTime CreatedUtc   { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("updatedUtc")]    public DateTime UpdatedUtc   { get; set; } = DateTime.UtcNow;

        public void Touch() => UpdatedUtc = DateTime.UtcNow;
    }
}
