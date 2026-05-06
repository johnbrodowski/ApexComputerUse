using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    /// <summary>
    /// One persistent record per element-hash. Either Note or Excluded (or both)
    /// must be meaningful — empty records are deleted by the store.
    /// </summary>
    public sealed class ElementAnnotation
    {
        [JsonPropertyName("hash")]       public string  Hash       { get; set; } = "";
        [JsonPropertyName("note")]       public string? Note       { get; set; }
        [JsonPropertyName("excluded")]   public bool    Excluded   { get; set; }
        [JsonPropertyName("controlType")]public string? ControlType{ get; set; }   // last-seen descriptor; informational
        [JsonPropertyName("name")]       public string? Name       { get; set; }
        [JsonPropertyName("automationId")]public string? AutomationId { get; set; }
        [JsonPropertyName("updatedUtc")] public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
