using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace ApexComputerUse
{
    /// <summary>
    /// JSON-driven 2-D drawing engine.
    ///
    /// The AI sends a POST /draw request whose body is a <see cref="DrawRequest"/>.
    /// The engine renders all shapes onto a bitmap and returns a base-64 PNG string.
    ///
    /// Canvas sources
    ///   "blank" / "white"  \- solid white background (default)
    ///   "black"            \- solid black background
    ///   "screen"           \- live screenshot of the primary monitor
    ///   "window"           \- resolved by CommandProcessor before this class is called
    ///   "element"          \- resolved by CommandProcessor before this class is called
    ///   &lt;base64&gt;           \- decode and paint on top of the supplied image
    ///
    /// Shape types
    ///   rect     \- rectangle (x, y, w, h)
    ///   ellipse  \- ellipse  (x, y, w, h)
    ///   circle   \- circle   (x, y, r)  \- x/y are the centre
    ///   line     \- line     (x, y) \-> (x2, y2)
    ///   arrow    \- line with arrowhead at (x2, y2)
    ///   text     \- label    (x, y, text)
    ///   polygon  \- closed shape (points = [x1,y1, x2,y2, \...])
    ///   triangle \- triangle (x, y, w, h) \- bounding-box anchored, top-centre apex
    ///   arc      \- open arc (x, y, w, h, start_angle, sweep_angle)
    /// </summary>
    public static class AIDrawingCommand
    {
        // \-\- Public request / shape models \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        public class DrawRequest
        {
            /// Canvas source: "blank", "white", "black", "screen", or a base-64 PNG.
            [JsonPropertyName("canvas")]      public string Canvas      { get; set; } = "blank";
            [JsonPropertyName("width")]       public int    Width       { get; set; } = 800;
            [JsonPropertyName("height")]      public int    Height      { get; set; } = 600;
            /// Background colour name or #RRGGBB (used when canvas = blank/white/black).
            [JsonPropertyName("background")]  public string Background  { get; set; } = "white";
            [JsonPropertyName("shapes")]      public List<ShapeCommand> Shapes { get; set; } = [];
            /// If true, show the result as a transparent on-screen overlay (like UiMapRenderer).
            [JsonPropertyName("overlay")]     public bool   Overlay     { get; set; } = false;
            /// How long the overlay stays visible in milliseconds. 0 = until Escape is pressed.
            [JsonPropertyName("overlay_ms")]  public int    OverlayMs   { get; set; } = 5000;
        }

        public class ShapeCommand
        {
            /// rect | ellipse | circle | line | arrow | text | polygon | triangle | arc
            [JsonPropertyName("type")]          public string   Type         { get; set; } = "rect";

            // Position / size
            [JsonPropertyName("x")]             public float    X            { get; set; }
            [JsonPropertyName("y")]             public float    Y            { get; set; }
            [JsonPropertyName("x2")]            public float    X2           { get; set; }
            [JsonPropertyName("y2")]            public float    Y2           { get; set; }
            [JsonPropertyName("w")]             public float    W            { get; set; } = 100;
            [JsonPropertyName("h")]             public float    H            { get; set; } = 60;
            /// Radius for circle (centre is x, y).
            [JsonPropertyName("r")]             public float    R            { get; set; } = 40;
            /// Polygon point pairs: [x1,y1, x2,y2, \...]
            [JsonPropertyName("points")]        public float[]? Points       { get; set; }
            /// Arc start angle in degrees, clockwise from 3 o'clock (GDI+ convention).
            [JsonPropertyName("start_angle")]   public float    StartAngle   { get; set; } = 0;
            /// Arc sweep angle in degrees. Positive = clockwise.
            [JsonPropertyName("sweep_angle")]   public float    SweepAngle   { get; set; } = 90;
            /// Rotation in degrees applied as a center-origin transform. 0 = no rotation.
            [JsonPropertyName("rotation")]      public float    Rotation     { get; set; } = 0;

            // Appearance
            [JsonPropertyName("color")]         public string   Color        { get; set; } = "red";
            [JsonPropertyName("fill")]          public bool     Fill         { get; set; } = false;
            [JsonPropertyName("stroke_width")]  public float    StrokeWidth  { get; set; } = 2;
            [JsonPropertyName("opacity")]       public float    Opacity      { get; set; } = 1.0f;
            [JsonPropertyName("corner_radius")] public float    CornerRadius { get; set; } = 0;
            [JsonPropertyName("dashed")]        public bool     Dashed       { get; set; } = false;

            // Text-specific
            [JsonPropertyName("text")]          public string   Text         { get; set; } = "";
            [JsonPropertyName("font_size")]     public float    FontSize     { get; set; } = 14;
            [JsonPropertyName("font_bold")]     public bool     FontBold     { get; set; } = false;
            /// Optional background fill behind the text label.
            [JsonPropertyName("background")]    public string?  Background   { get; set; }
            /// "left" | "center" | "right" \- horizontal alignment anchor at x.
            [JsonPropertyName("align")]         public string   Align        { get; set; } = "left";
        }

        // \-\- Entry point \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>Render the request and return a base-64 PNG string (no data-URI prefix).</summary>
        public static string Render(DrawRequest req)
        {
            int w = Math.Clamp(req.Width,  1, 4096);
            int h = Math.Clamp(req.Height, 1, 4096);

            using Bitmap   bmp = CreateCanvas(req.Canvas, req.Background, w, h);
            using Graphics g   = Graphics.FromImage(bmp);

            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;

            foreach (ShapeCommand shape in req.Shapes)
                DrawShape(g, shape);

            return BitmapToBase64(bmp);
        }

        /// <summary>
        /// Render a single shape onto an existing Graphics context.
        /// Used by the WinForms SceneCanvas to paint shapes directly in OnPaint
        /// without going through the full bitmap pipeline.
        /// </summary>
        internal static void RenderShapeTo(Graphics g, ShapeCommand s) => DrawShape(g, s);

        /// <summary>Parse a JSON string into a <see cref="DrawRequest"/>.</summary>
        public static DrawRequest ParseRequest(string json) =>
            JsonSerializer.Deserialize<DrawRequest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new DrawRequest();

        // \-\- Built-in demo scene \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>
        /// Returns a pre-built space-scene <see cref="DrawRequest"/> that exercises
        /// every shape type and a wide range of colours.
        /// </summary>
        public static DrawRequest BuildSpaceScene() => new()
        {
            Canvas     = "blank",
            Background = "#000822",
            Width      = 800,
            Height     = 500,
            Shapes     =
            [
                // \-\- Sky layers \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                // Horizon amber glow
                new() { Type="rect", X=0, Y=350, W=800, H=150, Color="#3d1200", Fill=true, Opacity=0.70f },

                // \-\- Stars \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="circle", X=42,  Y=35,  R=2.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=118, Y=18,  R=1.5f, Color="#FFFFD0", Fill=true },
                new() { Type="circle", X=198, Y=58,  R=2.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=345, Y=22,  R=1.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=448, Y=72,  R=2.0f, Color="#FFFFD0", Fill=true },
                new() { Type="circle", X=548, Y=18,  R=1.5f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=638, Y=38,  R=2.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=708, Y=62,  R=1.0f, Color="#FFFFD0", Fill=true },
                new() { Type="circle", X=28,  Y=82,  R=1.5f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=168, Y=102, R=2.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=298, Y=42,  R=1.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=498, Y=48,  R=2.0f, Color="#FFFFD0", Fill=true },
                new() { Type="circle", X=728, Y=28,  R=1.5f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=88,  Y=128, R=1.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=418, Y=12,  R=1.5f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=778, Y=88,  R=2.0f, Color="#FFFFD0", Fill=true },
                new() { Type="circle", X=258, Y=15,  R=1.0f, Color="#FFFFFF", Fill=true },
                new() { Type="circle", X=568, Y=55,  R=1.5f, Color="#FFFFFF", Fill=true },

                // \-\- Moon (upper-left) \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="circle", X=132, Y=118, R=54,   Color="#E8D4A0", Fill=true },
                // Craters
                new() { Type="circle", X=110, Y=100, R=10,   Color="#C8B070", Fill=true },
                new() { Type="circle", X=148, Y=128, R=7,    Color="#C8B070", Fill=true },
                new() { Type="circle", X=122, Y=142, R=5,    Color="#C8B070", Fill=true },
                new() { Type="circle", X=155, Y=105, R=4,    Color="#C8B070", Fill=true },

                // \-\- Planet (upper-right) \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                // Body
                new() { Type="circle", X=652, Y=152, R=88,   Color="#5B21B6", Fill=true },
                // Lighter cap highlight
                new() { Type="circle", X=620, Y=122, R=34,   Color="#8B5CF6", Fill=true, Opacity=0.55f },
                // Dark equatorial band
                new() { Type="ellipse", X=600, Y=160, W=104, H=20, Color="#3B0764", Fill=true, Opacity=0.60f },
                // Ring behind planet (draw first so it appears behind \- we can't z-sort, so approximate)
                new() { Type="ellipse", X=554, Y=144, W=196, H=38, Color="#B8860B", Fill=false, StrokeWidth=6 },
                // Small moon orbiting the planet
                new() { Type="circle", X=760, Y=108, R=16,   Color="#94A3B8", Fill=true },
                new() { Type="circle", X=755, Y=103, R=6,    Color="#64748B", Fill=true },

                // \-\- Background mountain silhouette \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="polygon",
                        Points=[0,500, 0,330, 70,258, 150,312, 222,268, 302,342,
                                370,288, 442,352, 512,258, 592,308, 660,248, 740,298,
                                800,268, 800,500],
                        Color="#0d1b2a", Fill=true },

                // \-\- Foreground mountain silhouette (darker) \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="polygon",
                        Points=[0,500, 0,398, 58,378, 122,402, 196,378, 268,404,
                                342,378, 418,402, 492,372, 568,398, 642,372,
                                718,398, 800,378, 800,500],
                        Color="#060d10", Fill=true },

                // \-\- Ground \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="rect", X=0, Y=462, W=800, H=38, Color="#020808", Fill=true },

                // \-\- Rocket \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                // Body
                new() { Type="polygon",
                        Points=[365,338, 360,260, 380,212, 400,260, 395,338],
                        Color="#DCDCDC", Fill=true },
                // Red accent stripe
                new() { Type="rect", X=361, Y=265, W=38, H=14, Color="#CC2200", Fill=true },
                // Porthole outer
                new() { Type="circle", X=380, Y=292, R=13, Color="#4FC3F7", Fill=true },
                // Porthole inner
                new() { Type="circle", X=380, Y=292, R=9,  Color="#0277BD", Fill=true },
                // Porthole glint
                new() { Type="circle", X=375, Y=287, R=3,  Color="#B3E5FC", Fill=true },
                // Left fin
                new() { Type="polygon", Points=[360,330, 338,362, 366,332], Color="#B0B0B0", Fill=true },
                // Right fin
                new() { Type="polygon", Points=[400,330, 422,362, 394,332], Color="#B0B0B0", Fill=true },
                // Outer flame
                new() { Type="polygon",
                        Points=[364,338, 354,375, 372,353, 380,380, 388,353, 406,375, 396,338],
                        Color="#FF5500", Fill=true },
                // Inner flame
                new() { Type="polygon",
                        Points=[368,338, 362,362, 380,350, 398,362, 392,338],
                        Color="#FFD700", Fill=true },

                // \-\- Shooting star \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="arrow", X=232, Y=74, X2=290, Y2=96,  Color="#FFFFFF", StrokeWidth=1.5f },
                new() { Type="circle", X=232, Y=74, R=2, Color="#FFFFFF", Fill=true },

                // \-\- Nebula cloud (large, very faint) \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="circle", X=265, Y=185, R=110, Color="#1a0066", Fill=true, Opacity=0.18f },
                new() { Type="circle", X=510, Y=95,  R=80,  Color="#003322", Fill=true, Opacity=0.14f },

                // \-\- Title text \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
                new() { Type="text", X=400, Y=10,
                        Text="*  DEEP  SPACE  *",
                        Color="#DDD0FF", FontSize=22, FontBold=true, Align="center" },
            ]
        };

        // \-\- Canvas factory \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private static Bitmap CreateCanvas(string canvas, string bgColor, int w, int h)
        {
            // Base-64 image supplied \- decode and use as background
            if (!string.IsNullOrWhiteSpace(canvas)
                && canvas != "blank" && canvas != "white"
                && canvas != "black" && canvas != "screen")
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(canvas);
                    using var ms  = new MemoryStream(bytes);
                    using var img = Image.FromStream(ms);
                    return new Bitmap(img);
                }
                catch { /* fall through to blank */ }
            }

            // Screen capture
            if (string.Equals(canvas, "screen", StringComparison.OrdinalIgnoreCase))
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                Rectangle bounds = screen?.Bounds ?? new Rectangle(0, 0, w, h);
                var shot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                using Graphics gs = Graphics.FromImage(shot);
                gs.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                return shot;
            }

            // Plain-colour canvas
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using Graphics gc = Graphics.FromImage(bmp);
            Color bg = string.Equals(canvas, "black", StringComparison.OrdinalIgnoreCase)
                     ? Color.Black
                     : ParseColor(bgColor, Color.White);
            gc.Clear(bg);
            return bmp;
        }

        // \-\- Shape dispatcher \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private static void DrawShape(Graphics g, ShapeCommand s)
        {
            GraphicsState state = g.Save();
            try
            {
                // Apply center-origin rotation when set
                if (s.Rotation != 0)
                {
                    var bb = ShapeBBoxInternal(s);
                    if (bb.HasValue)
                    {
                        float cx = bb.Value.X + bb.Value.Width  / 2f;
                        float cy = bb.Value.Y + bb.Value.Height / 2f;
                        g.TranslateTransform(cx, cy);
                        g.RotateTransform(s.Rotation);
                        g.TranslateTransform(-cx, -cy);
                    }
                }

                switch (s.Type.ToLowerInvariant())
                {
                    case "rect":     DrawRect(g, s);              break;
                    case "ellipse":  DrawEllipse(g, s);           break;
                    case "circle":   DrawCircle(g, s);            break;
                    case "line":     DrawLine(g, s, arrow: false); break;
                    case "arrow":    DrawLine(g, s, arrow: true);  break;
                    case "text":     DrawText(g, s);              break;
                    case "polygon":  DrawPolygon(g, s);           break;
                    case "triangle": DrawTriangle(g, s);          break;
                    case "arc":      DrawArc(g, s);               break;
                }
            }
            finally { g.Restore(state); }
        }

        // \-\- Individual shape renderers \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private static void DrawRect(Graphics g, ShapeCommand s)
        {
            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);

            if (s.CornerRadius > 0)
            {
                using GraphicsPath path = RoundedRect(s.X, s.Y, s.W, s.H, s.CornerRadius);
                if (s.Fill)
                    using (var br = new SolidBrush(color)) g.FillPath(br, path);
                using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
                g.DrawPath(pen, path);
            }
            else
            {
                var rect = new RectangleF(s.X, s.Y, s.W, s.H);
                if (s.Fill)
                    using (var br = new SolidBrush(color)) g.FillRectangle(br, rect);
                using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        private static void DrawEllipse(Graphics g, ShapeCommand s)
        {
            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);
            var rect = new RectangleF(s.X, s.Y, s.W, s.H);
            if (s.Fill)
                using (var br = new SolidBrush(color)) g.FillEllipse(br, rect);
            using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
            g.DrawEllipse(pen, rect);
        }

        private static void DrawCircle(Graphics g, ShapeCommand s)
        {
            // x, y = centre; r = radius
            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);
            var rect = new RectangleF(s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2);
            if (s.Fill)
                using (var br = new SolidBrush(color)) g.FillEllipse(br, rect);
            using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
            g.DrawEllipse(pen, rect);
        }

        private static void DrawLine(Graphics g, ShapeCommand s, bool arrow)
        {
            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);
            using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
            if (arrow)
            {
                float scale = Math.Max(1f, s.StrokeWidth);
                pen.CustomEndCap = new AdjustableArrowCap(
                    4f * scale / s.StrokeWidth,
                    5f * scale / s.StrokeWidth);
            }
            g.DrawLine(pen, s.X, s.Y, s.X2, s.Y2);
        }

        private static void DrawText(Graphics g, ShapeCommand s)
        {
            if (string.IsNullOrEmpty(s.Text)) return;

            FontStyle style = s.FontBold ? FontStyle.Bold : FontStyle.Regular;
            using Font font = new("Segoe UI", Math.Max(6f, s.FontSize), style, GraphicsUnit.Pixel);
            Color color     = ApplyOpacity(ParseColor(s.Color, Color.Black), s.Opacity);

            SizeF size = g.MeasureString(s.Text, font);
            float tx = s.Align switch
            {
                "center" => s.X - size.Width  / 2f,
                "right"  => s.X - size.Width,
                _        => s.X
            };

            // Optional background pill behind the label
            if (!string.IsNullOrWhiteSpace(s.Background))
            {
                Color bg = ApplyOpacity(ParseColor(s.Background!, Color.White), s.Opacity * 0.85f);
                using var bgBr = new SolidBrush(bg);
                g.FillRectangle(bgBr, tx - 3, s.Y - 2, size.Width + 6, size.Height + 4);
            }

            using SolidBrush br = new(color);
            g.DrawString(s.Text, font, br, tx, s.Y);
        }

        private static void DrawPolygon(Graphics g, ShapeCommand s)
        {
            if (s.Points == null || s.Points.Length < 4) return;

            var pts = new List<PointF>();
            for (int i = 0; i + 1 < s.Points.Length; i += 2)
                pts.Add(new PointF(s.Points[i], s.Points[i + 1]));
            if (pts.Count < 2) return;

            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);
            if (s.Fill)
                using (var br = new SolidBrush(color)) g.FillPolygon(br, pts.ToArray());
            using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
            g.DrawPolygon(pen, pts.ToArray());
        }

        private static void DrawTriangle(Graphics g, ShapeCommand s)
        {
            var pts = new PointF[]
            {
                new(s.X + s.W / 2f, s.Y),          // top-centre apex
                new(s.X,            s.Y + s.H),     // bottom-left
                new(s.X + s.W,      s.Y + s.H)      // bottom-right
            };
            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);
            if (s.Fill)
                using (var br = new SolidBrush(color)) g.FillPolygon(br, pts);
            using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
            g.DrawPolygon(pen, pts);
        }

        private static void DrawArc(Graphics g, ShapeCommand s)
        {
            Color color = ApplyOpacity(ParseColor(s.Color, Color.Red), s.Opacity);
            var rect    = new RectangleF(s.X, s.Y, Math.Max(1f, s.W), Math.Max(1f, s.H));
            using Pen pen = MakePen(color, s.StrokeWidth, s.Dashed);
            g.DrawArc(pen, rect, s.StartAngle, s.SweepAngle);
        }

        // \-\- Bounding-box helper (shared by renderer and WinForms editor) \-\-

        /// <summary>
        /// Returns the axis-aligned bounding rectangle of the shape in scene coordinates,
        /// ignoring any rotation transform. Returns null for degenerate/unknown shapes.
        /// </summary>
        internal static RectangleF? ShapeBBoxInternal(ShapeCommand s) => s.Type.ToLowerInvariant() switch
        {
            "rect" or "ellipse" or "triangle" or "arc"
                => new RectangleF(s.X, s.Y, Math.Max(1f, s.W), Math.Max(1f, s.H)),
            "circle"
                => new RectangleF(s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2),
            "line" or "arrow"
                => new RectangleF(
                       Math.Min(s.X, s.X2), Math.Min(s.Y, s.Y2),
                       Math.Max(1f, Math.Abs(s.X2 - s.X)),
                       Math.Max(1f, Math.Abs(s.Y2 - s.Y))),
            "text"
                => new RectangleF(s.X, s.Y,
                       (s.Text?.Length ?? 4) * (s.FontSize > 0 ? s.FontSize : 14) * 0.6f,
                       (s.FontSize > 0 ? s.FontSize : 14) + 4),
            "polygon" when s.Points != null && s.Points.Length >= 4
                => PolygonBBoxF(s.Points),
            _ => null
        };

        private static RectangleF PolygonBBoxF(float[] pts)
        {
            float minX = pts[0], maxX = pts[0], minY = pts[1], maxY = pts[1];
            for (int i = 0; i + 1 < pts.Length; i += 2)
            {
                if (pts[i]     < minX) minX = pts[i];
                if (pts[i]     > maxX) maxX = pts[i];
                if (pts[i + 1] < minY) minY = pts[i + 1];
                if (pts[i + 1] > maxY) maxY = pts[i + 1];
            }
            return new RectangleF(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
        }

        // \-\- Helpers \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private static Pen MakePen(Color color, float width, bool dashed)
        {
            var pen = new Pen(color, Math.Max(0.5f, width));
            if (dashed) pen.DashStyle = DashStyle.Dash;
            return pen;
        }

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            r = Math.Min(r, Math.Min(w, h) / 2f);
            var path = new GraphicsPath();
            path.AddArc(x,           y,           r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r*2, y,           r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r*2, y + h - r*2, r * 2, r * 2,   0, 90);
            path.AddArc(x,           y + h - r*2, r * 2, r * 2,  90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color ParseColor(string? name, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;

            // #RRGGBB or #AARRGGBB
            if (name.StartsWith('#'))
            {
                try
                {
                    string hex = name.TrimStart('#');
                    uint v = Convert.ToUInt32(hex, 16);
                    return hex.Length == 8
                        ? Color.FromArgb((int)v)
                        : Color.FromArgb(255,
                              (int)((v >> 16) & 0xFF),
                              (int)((v >>  8) & 0xFF),
                              (int)( v        & 0xFF));
                }
                catch { return fallback; }
            }

            // rgb(r,g,b)
            if (name.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string[] parts = name[4..^1].Split(',');
                    return Color.FromArgb(int.Parse(parts[0].Trim()),
                                         int.Parse(parts[1].Trim()),
                                         int.Parse(parts[2].Trim()));
                }
                catch { return fallback; }
            }

            Color named = Color.FromName(name);
            return named.IsKnownColor ? named : fallback;
        }

        private static Color ApplyOpacity(Color c, float opacity) =>
            Color.FromArgb((int)(Math.Clamp(opacity, 0f, 1f) * c.A), c.R, c.G, c.B);

        private static string BitmapToBase64(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

        // \-\- Screen overlay \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>
        /// Render the <see cref="DrawRequest"/> and show the result as a transparent,
        /// click-through, topmost overlay \- identical technique to UiMapRenderer.ShowOverlay.
        /// Must be called on the UI thread (use BeginInvoke from non-UI contexts).
        /// </summary>
        public static void ShowOverlay(DrawRequest req)
        {
            // Always capture the live screen so shapes are composited on top of
            // whatever is currently on screen, regardless of the canvas setting.
            // (A blank-canvas draw still looks useful floating over the desktop.)
            int w = Math.Clamp(req.Width,  1, 4096);
            int h = Math.Clamp(req.Height, 1, 4096);

            using Bitmap bmp = CreateCanvas(req.Canvas, req.Background, w, h);
            using Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;

            foreach (ShapeCommand shape in req.Shapes)
                DrawShape(g, shape);

            // Clone so the overlay form owns its own copy
            var overlayBitmap = (Bitmap)bmp.Clone();
            var form = new DrawOverlayForm(overlayBitmap, req.OverlayMs);
            form.Show();
        }

        // \-\- Overlay form \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private sealed class DrawOverlayForm : Form
        {
            private const int GWL_EXSTYLE     = -20;
            private const int WS_EX_LAYERED   = 0x00080000;
            private const int WS_EX_TRANSPARENT = 0x00000020;
            private const int WS_EX_TOOLWINDOW = 0x00000080;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            private readonly Bitmap _bitmap;
            private readonly System.Windows.Forms.Timer? _closeTimer;

            public DrawOverlayForm(Bitmap bitmap, int durationMs)
            {
                _bitmap = bitmap;

                FormBorderStyle = FormBorderStyle.None;
                StartPosition   = FormStartPosition.Manual;
                Bounds          = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                TopMost         = true;
                ShowInTaskbar   = false;
                BackColor       = Color.Magenta;
                TransparencyKey = Color.Magenta;
                DoubleBuffered  = true;

                // Close on Escape
                KeyPreview  = true;
                KeyDown    += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

                // Auto-close timer
                if (durationMs > 0)
                {
                    _closeTimer          = new System.Windows.Forms.Timer { Interval = durationMs };
                    _closeTimer.Tick    += (_, _) => { _closeTimer.Stop(); Close(); };
                    _closeTimer.Start();
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                // Make the window click-through so the user can still interact with whatever is behind it
                int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
                SetWindowLong(Handle, GWL_EXSTYLE,
                    exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                // Draw the rendered bitmap over the transparent form.
                // For a screen-sized canvas the shapes line up exactly with on-screen coordinates.
                // For a smaller canvas it appears top-left; the rest of the form is transparent.
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawImage(_bitmap, 0, 0);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _closeTimer?.Stop();
                    _closeTimer?.Dispose();
                    _bitmap.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
