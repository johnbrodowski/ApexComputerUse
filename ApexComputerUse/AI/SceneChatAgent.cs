using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ApexComputerUse
{
    /// <summary>
    /// Bridges the SceneEditorForm's collab chat dock to the AI.
    /// User message → AI (with scene JSON context) → streamed tokens back to the log.
    /// Any ```json fenced block of the form <c>{"ops":[…]}</c> in the reply is
    /// parsed and applied to the SceneStore, so the canvas updates live.
    /// </summary>
    public sealed class SceneChatAgent
    {
        private readonly SceneEditorForm _editor;
        private readonly SceneStore      _store;
        private readonly AiChatService   _chat;

        private readonly StringBuilder _replyBuffer = new();
        private int _turn;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly Regex JsonBlockRegex =
            new(@"```json\s*(\{[\s\S]*?\})\s*```", RegexOptions.Compiled);

        public SceneChatAgent(SceneEditorForm editor, SceneStore store, AiChatService chat)
        {
            _editor = editor;
            _store  = store;
            _chat   = chat;
            _editor.ChatMessageSubmitted += OnUserMessage;
        }

        private async void OnUserMessage(string text, string? sceneId)
        {
            string prompt = BuildPrompt(text, sceneId);

            _replyBuffer.Clear();
            _editor.BeginAiLine();
            _turn++;

            try
            {
                await _chat.SendAsync(
                    prompt,
                    onToken:    t  => { _replyBuffer.Append(t); _editor.AppendChatStream(t); },
                    onComplete: _  => FinishReply(sceneId),
                    onError:    m  => { _editor.AppendChatStream($"[error: {m}]"); _editor.EndChatLine(); });
            }
            catch (Exception ex)
            {
                _editor.AppendChatStream($"[exception: {ex.Message}]");
                _editor.EndChatLine();
            }
        }

        private void FinishReply(string? sceneId)
        {
            _editor.EndChatLine();

            if (sceneId == null) return;
            int applied = ApplyOps(_replyBuffer.ToString(), sceneId);
            if (applied > 0)
                _editor.AppendChatLog("sys", $"applied {applied} scene op{(applied == 1 ? "" : "s")}");
        }

        // ── Prompt ────────────────────────────────────────────────────────

        private string BuildPrompt(string userText, string? sceneId)
        {
            var sb = new StringBuilder();

            // Schema preamble only on the first turn — session retains context.
            if (_turn == 0)
            {
                sb.AppendLine(
                    "You are collaborating with a user on a 2D shape scene. Reply conversationally. " +
                    "To modify the scene, include exactly one fenced ```json block of the form:");
                sb.AppendLine("```json");
                sb.AppendLine("{\"ops\":[");
                sb.AppendLine("  {\"action\":\"add-shape\",   \"layer_id\":\"<id>\", \"shape\":{\"type\":\"rect|ellipse|circle|line|arrow|text|triangle|arc\",\"x\":0,\"y\":0,\"w\":100,\"h\":80,\"color\":\"#rrggbb\",\"fill\":true}},");
                sb.AppendLine("  {\"action\":\"update-shape\",\"layer_id\":\"<id>\",\"shape_id\":\"<id>\",\"shape\":{…full replacement…}},");
                sb.AppendLine("  {\"action\":\"delete-shape\",\"layer_id\":\"<id>\",\"shape_id\":\"<id>\"},");
                sb.AppendLine("  {\"action\":\"add-layer\",   \"name\":\"<name>\"}");
                sb.AppendLine("]}");
                sb.AppendLine("```");
                sb.AppendLine("Omit the block entirely if no change is requested. Keep prose brief.");
                sb.AppendLine();
            }

            if (sceneId != null)
            {
                var scene = _store.GetScene(sceneId);
                if (scene != null)
                {
                    sb.AppendLine("Current scene:");
                    sb.AppendLine("```json");
                    sb.AppendLine(JsonSerializer.Serialize(scene, JsonOpts));
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("User: " + userText);
            return sb.ToString();
        }

        // ── Op application ────────────────────────────────────────────────

        private int ApplyOps(string reply, string sceneId)
        {
            int applied = 0;
            foreach (Match m in JsonBlockRegex.Matches(reply))
            {
                Envelope? env;
                try { env = JsonSerializer.Deserialize<Envelope>(m.Groups[1].Value, JsonOpts); }
                catch (Exception ex)
                {
                    _editor.AppendChatLog("sys", $"could not parse ops json: {ex.Message}");
                    continue;
                }
                if (env?.Ops == null) continue;

                foreach (var op in env.Ops)
                {
                    try { if (ApplyOne(op, sceneId)) applied++; }
                    catch (Exception ex)
                    {
                        _editor.AppendChatLog("sys", $"op '{op.Action}' failed: {ex.Message}");
                    }
                }
            }
            return applied;
        }

        private bool ApplyOne(Op op, string sceneId)
        {
            switch ((op.Action ?? "").ToLowerInvariant())
            {
                case "add-shape":
                    if (op.Shape == null) return false;
                    _store.AddShape(sceneId, op.LayerId ?? DefaultLayerId(sceneId), op.Shape);
                    return true;

                case "update-shape":
                    if (op.Shape == null || op.ShapeId == null) return false;
                    _store.UpdateShape(sceneId, op.LayerId ?? DefaultLayerId(sceneId),
                                       op.ShapeId, op.Shape);
                    return true;

                case "delete-shape":
                    if (op.ShapeId == null) return false;
                    _store.DeleteShape(sceneId, op.LayerId ?? DefaultLayerId(sceneId), op.ShapeId);
                    return true;

                case "add-layer":
                    _store.AddLayer(sceneId, op.Name ?? "Layer");
                    return true;

                default:
                    _editor.AppendChatLog("sys", $"unknown op '{op.Action}'");
                    return false;
            }
        }

        private string DefaultLayerId(string sceneId)
        {
            var scene = _store.GetScene(sceneId)
                        ?? throw new InvalidOperationException($"scene {sceneId} not found");
            if (scene.Layers.Count == 0)
                return _store.AddLayer(sceneId, "Layer 1").Id;
            return scene.Layers.OrderBy(l => l.ZIndex).First().Id;
        }

        // ── Op envelope ───────────────────────────────────────────────────

        private sealed class Envelope
        {
            [JsonPropertyName("ops")] public List<Op>? Ops { get; set; }
        }

        private sealed class Op
        {
            [JsonPropertyName("action")]   public string? Action  { get; set; }
            [JsonPropertyName("layer_id")] public string? LayerId { get; set; }
            [JsonPropertyName("shape_id")] public string? ShapeId { get; set; }
            [JsonPropertyName("name")]     public string? Name    { get; set; }
            [JsonPropertyName("shape")]    public AIDrawingCommand.ShapeCommand? Shape { get; set; }
        }
    }
}
