using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        private CommandResponse CmdOcr(CommandRequest req)
        {
            if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
            _ocr ??= new OcrHelper();

            if (!string.IsNullOrWhiteSpace(req.Value))
            {
                var parts = req.Value!.Split(',');
                if (parts.Length == 4)
                {
                    if (!int.TryParse(parts[0].Trim(), out int rx) ||
                        !int.TryParse(parts[1].Trim(), out int ry) ||
                        !int.TryParse(parts[2].Trim(), out int rw) ||
                        !int.TryParse(parts[3].Trim(), out int rh) ||
                        rw <= 0 || rh <= 0)
                        return Fail("OCR region must be four integers: x,y,width,height with width and height > 0.");
                    var region = new System.Drawing.Rectangle(rx, ry, rw, rh);
                    var r = _ocr.OcrElementRegion(CurrentElement, region);
                    return Ok($"OCR region (confidence {r.Confidence:P1})", r.Text);
                }
            }

            var result = _ocr.OcrElement(CurrentElement);
            return Ok($"OCR (confidence {result.Confidence:P1})", result.Text);
        }

        private CommandResponse CmdCapture(CommandRequest req)
        {
            var target = (req.Action ?? "element").ToLowerInvariant();

            switch (target)
            {
                case "screen":
                    return Ok("Captured screen", _helper.CaptureScreenToBase64());

                case "window":
                    if (CurrentWindow == null) return Fail("No window selected. Use 'find' first.");
                    return Ok($"Captured window: {CurrentWindow.Title}",
                              _helper.CaptureElementToBase64(CurrentWindow));

                case "elements":
                    if (string.IsNullOrWhiteSpace(req.Value))
                        return Fail("Provide element IDs in value= (comma-separated numeric IDs from /elements).");
                    var elems = new List<AutomationElement>();
                    foreach (var part in req.Value!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        if (int.TryParse(part.Trim(), out int id) && _elementMap.TryGetValue(id, out var el))
                            elems.Add(el);
                    if (elems.Count == 0) return Fail("No valid element IDs found in map. Run 'elements' first.");
                    return Ok($"Captured {elems.Count} element(s)", _helper.StitchElementsToBase64(elems));

                default: // "element"
                    if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
                    return Ok("Captured element", _helper.CaptureElementToBase64(CurrentElement));
            }
        }

        private CommandResponse CmdDraw(CommandRequest req)
        {
            string json = !string.IsNullOrWhiteSpace(req.Value)  ? req.Value!
                        : !string.IsNullOrWhiteSpace(req.Prompt) ? req.Prompt!
                        : "";

            if (string.IsNullOrWhiteSpace(json))
                return Fail("'draw' requires a JSON DrawRequest in the value field. " +
                            "Example: {\"canvas\":\"blank\",\"width\":800,\"height\":600," +
                            "\"shapes\":[{\"type\":\"circle\",\"x\":400,\"y\":300,\"r\":80,\"color\":\"royalblue\",\"fill\":true}]}");

            try
            {
                var drawReq = AIDrawingCommand.ParseRequest(json);

                // Resolve canvas sources that need the UI automation helper
                if (string.Equals(drawReq.Canvas, "window", StringComparison.OrdinalIgnoreCase))
                {
                    if (CurrentWindow == null) return Fail("No window selected. Use 'find' first.");
                    drawReq.Canvas = _helper.CaptureElementToBase64(CurrentWindow);
                }
                else if (string.Equals(drawReq.Canvas, "element", StringComparison.OrdinalIgnoreCase))
                {
                    if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
                    drawReq.Canvas = _helper.CaptureElementToBase64(CurrentElement);
                }

                string base64 = AIDrawingCommand.Render(drawReq);

                if (drawReq.Overlay)
                {
                    // ShowOverlay must run on the UI thread
                    System.Windows.Forms.Application.OpenForms[0]?.BeginInvoke(
                        () => AIDrawingCommand.ShowOverlay(drawReq));
                }

                int ms = drawReq.OverlayMs;
                string overlayNote = drawReq.Overlay
                    ? (ms > 0 ? $" Overlay showing for {ms / 1000.0:0.#}s (Esc to dismiss)."
                               : " Overlay showing - press Esc to dismiss.")
                    : "";
                return Ok($"Drawing rendered ({drawReq.Shapes.Count} shape(s)).{overlayNote}", base64);
            }
            catch (Exception ex)
            {
                return Fail($"Draw error: {ex.Message}");
            }
        }

        private CommandResponse CmdRenderMap()
        {
            if (CurrentWindow == null) return Fail("No window selected. Use 'find window=X' first.");

            var elemResponse = CmdListElements(new CommandRequest { Command = "elements" });
            if (!elemResponse.Success || string.IsNullOrWhiteSpace(elemResponse.Data))
                return Fail("Could not scan elements: " + elemResponse.Message);

            string json = elemResponse.Data!;

            var renderer = new UiMapRenderer(new[]
            {
                "Button", "Document", "Text", "Window", "Pane", "MenuItem", "TitleBar",
                "CheckBox", "ComboBox", "DataGrid", "Edit", "Group", "Hyperlink", "List",
                "ListItem", "Menu", "MenuBar", "Slider", "Spinner", "StatusBar", "ScrollBar",
                "Tab", "ToolTip", "ToolBar", "TabItem", "Image", "AppBar", "Calendar",
                "Custom", "DataItem", "Header", "HeaderItem", "ProgressBar", "RadioButton",
                "SemanticZoom", "Separator", "SplitButton", "Table", "Thumb", "Tree",
                "TreeItem", "Unknown"
            });

            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

            string tmp = Path.GetTempFileName();
            try
            {
                renderer.Render(json, tmp, screen.Width, screen.Height);
                string b64 = Convert.ToBase64String(File.ReadAllBytes(tmp));
                return Ok($"UI map: {_elementMap.Count} element(s)", b64);
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* best effort */ }
            }
        }

    }
}

