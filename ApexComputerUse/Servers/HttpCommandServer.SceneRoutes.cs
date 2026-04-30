using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {

        // Scene REST routes 

        /// <summary>
        /// Returns a result if the path matches a /scenes/* route, null otherwise.
        /// </summary>
        private ApexResult? TryHandleSceneRoute(string method, string path,
                                                 string body, HttpListenerRequest req)
        {
            // Segments: ["", "scenes", id?, "layers"?, lid?, "shapes"?, sid?]
            var seg = path.Split('/');
            if (seg.Length < 2 || seg[1] != "scenes") return null;

            try
            {
                // POST /scenes/{id}/shapes/{sid}/move
                if (seg.Length == 6 && seg[3] == "shapes" && seg[5] == "move" && method == "POST")
                {
                    using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                    string targetLayerId = d.RootElement.Str("target_layer_id") ?? "";
                    var ss = _store.MoveShapeToLayer(seg[2], seg[4], targetLayerId);
                    return Ok("scene/shapes/move", "shape", JsonSerializer.Serialize(ss, FormatAdapter.s_compact));
                }

                switch (seg.Length)
                {
                    // GET /scenes   POST /scenes
                    case 2:
                        if (method == "GET")
                        {
                            var list = _store.ListScenes();
                            return Ok("scenes/list", "scenes", JsonSerializer.Serialize(list, FormatAdapter.s_compact));
                        }
                        if (method == "POST")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var scene = _store.CreateScene(
                                d.RootElement.Str("name") ?? "Untitled",
                                d.RootElement.Int("width") ?? 800,
                                d.RootElement.Int("height") ?? 600,
                                d.RootElement.Str("background") ?? "white");
                            return Ok("scenes/create", "scene", JsonSerializer.Serialize(scene, FormatAdapter.s_compact));
                        }
                        break;

                    // GET/PUT/DELETE /scenes/{id}   GET /scenes/{id}/render
                    case 3:
                    {
                        string id = seg[2];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(id)
                                        ?? throw new KeyNotFoundException($"Scene '{id}' not found.");
                            return Ok("scenes/get", "scene", JsonSerializer.Serialize(scene, FormatAdapter.s_compact));
                        }
                        if (method == "PUT" || method == "PATCH")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var scene = _store.UpdateSceneMeta(id,
                                d.RootElement.Str("name"),
                                d.RootElement.Int("width"),
                                d.RootElement.Int("height"),
                                d.RootElement.Str("background"));
                            return Ok("scenes/update", "scene", JsonSerializer.Serialize(scene, FormatAdapter.s_compact));
                        }
                        if (method == "DELETE")
                        {
                            _store.DeleteScene(id);
                            return Ok("scenes/delete", "deleted", id);
                        }
                        break;
                    }

                    case 4 when seg[3] == "render":
                    {
                        string base64 = _store.RenderScene(seg[2]);
                        return Ok("scenes/render", "result", base64);
                    }

                    // GET/POST /scenes/{id}/layers
                    case 4 when seg[3] == "layers":
                    {
                        string sceneId = seg[2];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(sceneId)
                                        ?? throw new KeyNotFoundException($"Scene '{sceneId}' not found.");
                            return Ok("scenes/layers/list", "layers", JsonSerializer.Serialize(scene.Layers));
                        }
                        if (method == "POST")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var layer = _store.AddLayer(sceneId, d.RootElement.Str("name") ?? "Layer");
                            return Ok("scenes/layers/add", "layer", JsonSerializer.Serialize(layer, FormatAdapter.s_compact));
                        }
                        break;
                    }

                    // GET/PUT/PATCH/DELETE /scenes/{id}/layers/{lid}
                    case 5 when seg[3] == "layers":
                    {
                        string sceneId  = seg[2];
                        string layerId  = seg[4];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(sceneId)
                                        ?? throw new KeyNotFoundException($"Scene '{sceneId}' not found.");
                            var layer = scene.Layers.FirstOrDefault(l => l.Id == layerId)
                                        ?? throw new KeyNotFoundException($"Layer '{layerId}' not found.");
                            return Ok("scenes/layers/get", "layer", JsonSerializer.Serialize(layer, FormatAdapter.s_compact));
                        }
                        if (method == "PUT" || method == "PATCH")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            float? opacity = d.RootElement.TryGetProperty("opacity", out var op)
                                             && op.ValueKind == JsonValueKind.Number
                                             ? (float?)op.GetSingle() : null;
                            int?   zIndex  = d.RootElement.Int("z_index") ?? d.RootElement.Int("zIndex");
                            bool?  visible = d.RootElement.Bool("visible");
                            bool?  locked  = d.RootElement.Bool("locked");
                            var    layer   = _store.UpdateLayer(sceneId, layerId,
                                                d.RootElement.Str("name"), visible, locked, opacity, zIndex);
                            return Ok("scenes/layers/update", "layer", JsonSerializer.Serialize(layer, FormatAdapter.s_compact));
                        }
                        if (method == "DELETE")
                        {
                            _store.DeleteLayer(sceneId, layerId);
                            return Ok("scenes/layers/delete", "deleted", layerId);
                        }
                        break;
                    }

                    // GET/POST /scenes/{id}/layers/{lid}/shapes
                    case 6 when seg[3] == "layers" && seg[5] == "shapes":
                    {
                        string sceneId = seg[2];
                        string layerId = seg[4];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(sceneId)
                                        ?? throw new KeyNotFoundException($"Scene '{sceneId}' not found.");
                            var layer = scene.Layers.FirstOrDefault(l => l.Id == layerId)
                                        ?? throw new KeyNotFoundException($"Layer '{layerId}' not found.");
                            return Ok("scenes/shapes/list", "shapes", JsonSerializer.Serialize(layer.Shapes));
                        }
                        if (method == "POST")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            string shapeJson = d.RootElement.TryGetProperty("shape", out var sp)
                                               ? sp.GetRawText() : body;
                            string? name = d.RootElement.Str("name");
                            var shapeCmd = JsonSerializer.Deserialize<AIDrawingCommand.ShapeCommand>(
                                shapeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                ?? throw new InvalidOperationException("Invalid shape JSON.");
                            var ss = _store.AddShape(sceneId, layerId, shapeCmd, name);
                            return Ok("scenes/shapes/add", "shape", JsonSerializer.Serialize(ss, FormatAdapter.s_compact));
                        }
                        break;
                    }

                    // GET/PUT/PATCH/DELETE /scenes/{id}/layers/{lid}/shapes/{sid}
                    case 7 when seg[3] == "layers" && seg[5] == "shapes":
                    {
                        string sceneId = seg[2];
                        string layerId = seg[4];
                        string shapeId = seg[6];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(sceneId)
                                        ?? throw new KeyNotFoundException($"Scene '{sceneId}' not found.");
                            var layer = scene.Layers.FirstOrDefault(l => l.Id == layerId)
                                        ?? throw new KeyNotFoundException($"Layer '{layerId}' not found.");
                            var shape = layer.Shapes.FirstOrDefault(s => s.Id == shapeId)
                                        ?? throw new KeyNotFoundException($"Shape '{shapeId}' not found.");
                            return Ok("scenes/shapes/get", "shape", JsonSerializer.Serialize(shape, FormatAdapter.s_compact));
                        }
                        if (method == "PUT")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            string shapeJson = d.RootElement.TryGetProperty("shape", out var sp)
                                               ? sp.GetRawText() : body;
                            string? name = d.RootElement.Str("name");
                            var shapeCmd = JsonSerializer.Deserialize<AIDrawingCommand.ShapeCommand>(
                                shapeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                ?? throw new InvalidOperationException("Invalid shape JSON.");
                            var ss = _store.UpdateShape(sceneId, layerId, shapeId, shapeCmd, name);
                            return Ok("scenes/shapes/update", "shape", JsonSerializer.Serialize(ss, FormatAdapter.s_compact));
                        }
                        if (method == "PATCH")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var ss = _store.PatchShapeGeometry(sceneId, layerId, shapeId,
                                d.RootElement.Float("x"),  d.RootElement.Float("y"),
                                d.RootElement.Float("x2"), d.RootElement.Float("y2"),
                                d.RootElement.Float("w"),  d.RootElement.Float("h"),
                                d.RootElement.TryGetProperty("points", out var pts)
                                    ? pts.EnumerateArray().Select(p => p.GetSingle()).ToArray() : null,
                                r:          d.RootElement.Float("r"),
                                visible:    d.RootElement.Bool("visible"),
                                locked:     d.RootElement.Bool("locked"),
                                zIndex:     d.RootElement.Int("z_index") ?? d.RootElement.Int("zIndex"),
                                name:       d.RootElement.Str("name"),
                                rotation:   d.RootElement.Float("rotation"),
                                startAngle: d.RootElement.Float("start_angle"),
                                sweepAngle: d.RootElement.Float("sweep_angle"));
                            return Ok("scenes/shapes/patch", "shape", JsonSerializer.Serialize(ss, FormatAdapter.s_compact));
                        }
                        if (method == "DELETE")
                        {
                            _store.DeleteShape(sceneId, layerId, shapeId);
                            return Ok("scenes/shapes/delete", "deleted", shapeId);
                        }
                        break;
                    }
                }
            }
            catch (KeyNotFoundException ex)
            {
                return new ApexResult { Success = false, Action = $"scenes {method} {path}", Error = ex.Message };
            }
            catch (Exception ex)
            {
                return new ApexResult { Success = false, Action = $"scenes {method} {path}", Error = ex.Message };
            }

            return null; // no match
        }

        private static ApexResult Ok(string action, string key, string value) =>
            new() { Success = true, Action = action,
                    Data = new Dictionary<string, string> { [key] = value } };

    }
}
