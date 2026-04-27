using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        private CommandResponse CmdScene(CommandRequest req)
        {
            if (SceneStore == null) return Fail("SceneStore not initialised.");

            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string action = (req.Action ?? "").ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "list":
                    {
                        var scenes = SceneStore.ListScenes()
                            .Select(s => new { s.Id, s.Name, s.Width, s.Height,
                                               shapes = s.Layers.Sum(l => l.Shapes.Count) });
                        return Ok("Scenes", System.Text.Json.JsonSerializer.Serialize(scenes, opts));
                    }

                    case "create":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<SceneCreateParams>(req.Value ?? "{}", opts)
                                ?? new SceneCreateParams();
                        var scene = SceneStore.CreateScene(p.Name ?? "Untitled",
                                        p.Width ?? 800, p.Height ?? 600, p.Background ?? "white");
                        return Ok($"Scene created: {scene.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(scene, opts));
                    }

                    case "get":
                    {
                        var scene = SceneStore.GetScene(req.Value?.Trim() ?? "")
                                    ?? throw new KeyNotFoundException($"Scene '{req.Value}' not found.");
                        return Ok(scene.Name, System.Text.Json.JsonSerializer.Serialize(scene, opts));
                    }

                    case "render":
                    {
                        string b64 = SceneStore.RenderScene(req.Value?.Trim() ?? "");
                        return Ok("Scene rendered", b64);
                    }

                    case "add-layer":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<LayerParams>(req.Value ?? "{}", opts)
                                ?? new LayerParams();
                        var layer = SceneStore.AddLayer(p.SceneId ?? "", p.Name ?? "Layer");
                        return Ok($"Layer added: {layer.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(layer, opts));
                    }

                    case "add-shape":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<ShapeParams>(req.Value ?? "{}", opts)
                                ?? new ShapeParams();
                        var ss = SceneStore.AddShape(p.SceneId ?? "", p.LayerId ?? "",
                                     p.Shape ?? new AIDrawingCommand.ShapeCommand(), p.Name);
                        return Ok($"Shape added: {ss.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(ss, opts));
                    }

                    case "update-shape":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<ShapeParams>(req.Value ?? "{}", opts)
                                ?? new ShapeParams();
                        var ss = SceneStore.UpdateShape(p.SceneId ?? "", p.LayerId ?? "",
                                     p.ShapeId ?? "", p.Shape ?? new AIDrawingCommand.ShapeCommand(),
                                     p.Name);
                        return Ok($"Shape updated: {ss.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(ss, opts));
                    }

                    case "delete-shape":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<ShapeParams>(req.Value ?? "{}", opts)
                                ?? new ShapeParams();
                        SceneStore.DeleteShape(p.SceneId ?? "", p.LayerId ?? "", p.ShapeId ?? "");
                        return Ok($"Shape deleted: {p.ShapeId}");
                    }

                    default:
                        return Fail($"Unknown scene action '{action}'. " +
                                    "Try: list, create, get, render, add-layer, add-shape, update-shape, delete-shape");
                }
            }
            catch (KeyNotFoundException ex) { return Fail(ex.Message); }
            catch (Exception ex)            { return Fail($"Scene error: {ex.Message}"); }
        }

        // ── Scene command parameter POCOs ─────────────────────────────────

        private class SceneCreateParams
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]       public string? Name       { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("width")]      public int?    Width      { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("height")]     public int?    Height     { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("background")] public string? Background { get; set; }
        }

        private class LayerParams
        {
            [System.Text.Json.Serialization.JsonPropertyName("scene_id")]   public string? SceneId { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("name")]       public string? Name    { get; set; }
        }

        private class ShapeParams
        {
            [System.Text.Json.Serialization.JsonPropertyName("scene_id")]   public string? SceneId  { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("layer_id")]   public string? LayerId  { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("shape_id")]   public string? ShapeId  { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("name")]       public string? Name     { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("shape")]      public AIDrawingCommand.ShapeCommand? Shape { get; set; }
        }

    }
}
