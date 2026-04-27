using FlaUI.Core.AutomationElements;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace ApexComputerUse
{

public class UiMapRenderer
{
    private readonly HashSet<string> _includedControlTypes;
    private readonly Dictionary<string, Color> _controlColors = new();

    public UiMapRenderer(IEnumerable<string> includedControlTypes)
    {
        _includedControlTypes = new HashSet<string>(
            includedControlTypes,
            StringComparer.OrdinalIgnoreCase);
    }

    public void Render(string json, string outputPath, int canvasWidth, int canvasHeight)
    {
        var elements = ParseElements(json);

        using var bitmap = new Bitmap(canvasWidth, canvasHeight);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        if (elements != null)
        {
            foreach (var element in elements)
                DrawElementRecursive(graphics, element);
        }

        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private void DrawElementRecursive(Graphics g, UiElement element)
    {
        if (element.BoundingRectangle == null)
            return;

        string name = element.Name;




        bool shouldDraw =
            _includedControlTypes.Count == 0 ||
            _includedControlTypes.Contains(element.ControlType);


        if (shouldDraw)
        {
            var color = GetColorForControlType(element.ControlType);

           using var pen = new Pen(color, 2);
           using var font = new Font("Segoe UI", 7);
           using var brush = new SolidBrush(color);
            // using var brush = new SolidBrush(Color.Black);

            var rect = new Rectangle(
                element.BoundingRectangle.X,
                element.BoundingRectangle.Y,
                element.BoundingRectangle.Width,
                element.BoundingRectangle.Height);

            g.DrawRectangle(pen, rect);

            // string label = $"{element.ControlType}\n{(string.IsNullOrWhiteSpace(element.Name) ? "<no name>" : element.Name)}";
            // label = $"{(string.IsNullOrWhiteSpace(name) ? "<no name>" : name)}";
            var label = $"{(string.IsNullOrWhiteSpace(name)  ? "" : name)}";


            g.DrawString(
                label,
                font,
                brush,
                rect.X + 2,
                rect.Y + 2);
        }

        if (element.Children != null)
        {
            foreach (var child in element.Children)
                if (child != null) DrawElementRecursive(g, child);
        }
    }

    private Color GetColorForControlType(string type)
    {
        if (_controlColors.TryGetValue(type, out var color))
            return color;

        // deterministic but visually distinct
        int hash = type.GetHashCode();
        var rand = new Random(hash);

        color = Color.FromArgb(
            255,
            rand.Next(40, 220),
            rand.Next(40, 220),
            rand.Next(40, 220));

        _controlColors[type] = color;
        return color;
    }

    // ============================
    // JSON MODELS
    // ============================

    private class UiElement
    {
        public string Name { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public BoundingRect BoundingRectangle { get; set; } = new();
        public List<UiElement?>? Children { get; set; }
        [JsonIgnore]
        public AutomationElement? AutomationElement { get; set; }
    }

    private class BoundingRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    // ============================
    // OVERLAY FUNCTIONALITY
    // ============================

    /// <summary>
    /// Shows a transparent overlay on screen with the UI elements drawn on it.
    /// </summary>
    /// <param name="json">JSON string containing UI elements</param>
    /// <param name="durationMs">Duration in milliseconds before auto-close (0 = manual close with Escape)</param>
    public void ShowOverlay(string json, int durationMs = 3000)
    {
        var elements = ParseElements(json);
        var overlay = new OverlayForm(elements, _includedControlTypes, _controlColors, GetColorForControlType, durationMs);
        overlay.Show();
    }

    /// <summary>
    /// Parses elements from JSON that is either a flat array (original format)
    /// or a single root object with nested children (JsonTreeExporter format).
    /// </summary>
    private static List<UiElement> ParseElements(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var trimmed = json.AsSpan().TrimStart();
        if (!trimmed.IsEmpty && trimmed[0] == '[')
            return JsonSerializer.Deserialize<List<UiElement>>(json, options) ?? [];
        var root = JsonSerializer.Deserialize<UiElement>(json, options);
        return root != null ? [root] : [];
    }


    private class OverlayForm : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;
        private const int WS_EX_TOOLWINDOW = 0x80;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;

        private readonly List<UiElement>? _elements;
        private readonly HashSet<string> _includedControlTypes;
        private readonly Dictionary<string, Color> _controlColors;
        private readonly Func<string, Color> _getColorFunc;
        private readonly System.Windows.Forms.Timer? _closeTimer;

        public OverlayForm(
            List<UiElement>? elements,
            HashSet<string> includedControlTypes,
            Dictionary<string, Color> controlColors,
            Func<string, Color> getColorFunc,
            int durationMs)
        {
            _elements = elements;
            _includedControlTypes = includedControlTypes;
            _controlColors = controlColors;
            _getColorFunc = getColorFunc;

            // Form setup for transparent overlay
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.DoubleBuffered = true;

            // Close on Escape key
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    this.Close();
            };

            // Auto-close timer
            if (durationMs > 0)
            {
                _closeTimer = new System.Windows.Forms.Timer();
                _closeTimer.Interval = durationMs;
                _closeTimer.Tick += (s, e) =>
                {
                    _closeTimer.Stop();
                    this.Close();
                };
                _closeTimer.Start();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Make window click-through
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (_elements != null)
            {
                foreach (var element in _elements)
                    DrawElementRecursive(e.Graphics, element);
            }
        }

        private void DrawElementRecursive(Graphics g, UiElement element)
        {
            if (element.BoundingRectangle == null)
                return;

            bool shouldDraw =
                _includedControlTypes.Count == 0 ||
                _includedControlTypes.Contains(element.ControlType);

            if (shouldDraw)
            {
                var color = _getColorFunc(element.ControlType);

                using var pen = new Pen(color, 2);
                using var font = new Font("Segoe UI", 8, FontStyle.Bold);
                using var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
                using var textBrush = new SolidBrush(color);

                var rect = new Rectangle(
                    element.BoundingRectangle.X,
                    element.BoundingRectangle.Y,
                    element.BoundingRectangle.Width,
                    element.BoundingRectangle.Height);

                g.DrawRectangle(pen, rect);

                string label = string.IsNullOrWhiteSpace(element.Name) ? element.ControlType : element.Name;

                // Draw label with background for readability
                var labelSize = g.MeasureString(label, font);
                var labelRect = new RectangleF(rect.X + 2, rect.Y + 2, labelSize.Width + 4, labelSize.Height + 2);
                g.FillRectangle(bgBrush, labelRect);
                g.DrawString(label, font, textBrush, rect.X + 4, rect.Y + 3);
            }

            if (element.Children != null)
            {
                foreach (var child in element.Children)
                {
                    if (child != null)
                        DrawElementRecursive(g, child);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closeTimer?.Stop();
                _closeTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

} // namespace ApexComputerUse
