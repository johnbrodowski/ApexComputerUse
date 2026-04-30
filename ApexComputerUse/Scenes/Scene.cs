using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    // \-\- Shape wrapper (adds editor metadata around ShapeCommand) \-\-\-\-\-\-\-\-\-\-

    public sealed class SceneShape
    {
        [JsonPropertyName("id")]      public string       Id      { get; set; } = SceneIds.New();
        [JsonPropertyName("name")]    public string       Name    { get; set; } = "";
        [JsonPropertyName("visible")] public bool         Visible { get; set; } = true;
        [JsonPropertyName("locked")]  public bool         Locked  { get; set; } = false;
        [JsonPropertyName("z_index")] public int          ZIndex  { get; set; } = 0;
        [JsonPropertyName("shape")]   public AIDrawingCommand.ShapeCommand Shape { get; set; } = new();
    }

    // \-\- Layer \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    public sealed class Layer
    {
        [JsonPropertyName("id")]      public string            Id      { get; set; } = SceneIds.New();
        [JsonPropertyName("name")]    public string            Name    { get; set; } = "Layer";
        [JsonPropertyName("z_index")] public int               ZIndex  { get; set; } = 0;
        [JsonPropertyName("visible")] public bool              Visible { get; set; } = true;
        [JsonPropertyName("locked")]  public bool              Locked  { get; set; } = false;
        [JsonPropertyName("opacity")] public float             Opacity { get; set; } = 1.0f;
        [JsonPropertyName("shapes")]  public List<SceneShape>  Shapes  { get; set; } = [];
    }

    // \-\- Scene \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    public sealed class Scene
    {
        [JsonPropertyName("id")]         public string      Id         { get; set; } = SceneIds.New();
        [JsonPropertyName("name")]       public string      Name       { get; set; } = "Untitled";
        [JsonPropertyName("width")]      public int         Width      { get; set; } = 800;
        [JsonPropertyName("height")]     public int         Height     { get; set; } = 600;
        [JsonPropertyName("background")] public string      Background { get; set; } = "white";
        [JsonPropertyName("layers")]     public List<Layer> Layers     { get; set; } = [];
        [JsonPropertyName("created_at")] public string      CreatedAt  { get; set; } = DateTimeOffset.UtcNow.ToString("O");
        [JsonPropertyName("updated_at")] public string      UpdatedAt  { get; set; } = DateTimeOffset.UtcNow.ToString("O");

        /// <summary>
        /// Returns all visible shapes in render order (layer z_index asc \-> shape z_index asc)
        /// with opacity multiplied by the containing layer's opacity.
        /// The returned ShapeCommands are clones \- safe to mutate without affecting the scene.
        /// </summary>
        public List<AIDrawingCommand.ShapeCommand> FlattenForRender()
        {
            var result = new List<AIDrawingCommand.ShapeCommand>();
            var opts   = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var layer in Layers.Where(l => l.Visible).OrderBy(l => l.ZIndex))
            {
                foreach (var ss in layer.Shapes.Where(s => s.Visible).OrderBy(s => s.ZIndex))
                {
                    // JSON round-trip clone \- maintenance-free as new fields are added
                    var json  = JsonSerializer.Serialize(ss.Shape, opts);
                    var clone = JsonSerializer.Deserialize<AIDrawingCommand.ShapeCommand>(json, opts)!;
                    clone.Opacity = clone.Opacity * layer.Opacity;
                    result.Add(clone);
                }
            }
            return result;
        }

        /// <summary>Build the DrawRequest needed by AIDrawingCommand.Render().</summary>
        public AIDrawingCommand.DrawRequest ToDrawRequest() => new()
        {
            Canvas     = "blank",
            Background = Background,
            Width      = Width,
            Height     = Height,
            Shapes     = FlattenForRender()
        };

        public void Touch() => UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
    }

    // \-\- Shared ID generator \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    internal static class SceneIds
    {
        public static string New() => Guid.NewGuid().ToString("N")[..8];
    }
}
