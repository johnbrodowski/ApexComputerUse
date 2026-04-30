using System.Drawing;
using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="AIDrawingCommand"/>.
/// Tests cover JSON request parsing, canvas source resolution, and GDI+ rendering
/// of each shape type.  The "screen" canvas source is skipped (requires a display).
/// </summary>
public class AIDrawingCommandTests
{
    // -- ParseRequest ------------------------------------------------------

    [Fact]
    public void ParseRequest_ValidJson_ReturnsDrawRequest()
    {
        const string json = """
            {
              "canvas": "blank",
              "width": 400,
              "height": 300,
              "background": "blue",
              "shapes": [
                { "type": "rect", "x": 10, "y": 20, "w": 100, "h": 50, "color": "red" }
              ]
            }
            """;

        var req = AIDrawingCommand.ParseRequest(json);

        Assert.Equal("blank",  req.Canvas);
        Assert.Equal(400,      req.Width);
        Assert.Equal(300,      req.Height);
        Assert.Equal("blue",   req.Background);
        Assert.Single(req.Shapes);
        Assert.Equal("rect",   req.Shapes[0].Type);
        Assert.Equal(10f,      req.Shapes[0].X);
    }

    [Fact]
    public void ParseRequest_CaseInsensitiveKeys_Parsed()
    {
        const string json = """{ "Canvas": "blank", "Width": 200, "HEIGHT": 150 }""";
        var req = AIDrawingCommand.ParseRequest(json);
        Assert.Equal("blank", req.Canvas);
        Assert.Equal(200,     req.Width);
        Assert.Equal(150,     req.Height);
    }

    [Fact]
    public void ParseRequest_EmptyJson_ReturnsDefaults()
    {
        var req = AIDrawingCommand.ParseRequest("{}");
        Assert.Equal("blank",  req.Canvas);
        Assert.Equal(800,      req.Width);
        Assert.Equal(600,      req.Height);
        Assert.Equal("white",  req.Background);
        Assert.Empty(req.Shapes);
    }

    [Fact]
    public void ParseRequest_InvalidJson_ThrowsJsonException()
    {
        // ParseRequest has no try/catch - invalid JSON propagates as JsonException.
        Assert.ThrowsAny<System.Text.Json.JsonException>(
            () => AIDrawingCommand.ParseRequest("not json at all {{{"));
    }

    [Fact]
    public void ParseRequest_AllShapeFields_RoundTrip()
    {
        const string json = """
            {
              "shapes": [{
                "type": "circle",
                "x": 50, "y": 60, "r": 25,
                "color": "#FF0000",
                "fill": true,
                "stroke_width": 3,
                "opacity": 0.8,
                "dashed": true,
                "rotation": 45
              }]
            }
            """;

        var s = AIDrawingCommand.ParseRequest(json).Shapes[0];
        Assert.Equal("circle", s.Type);
        Assert.Equal(50f,      s.X);
        Assert.Equal(25f,      s.R);
        Assert.True(s.Fill);
        Assert.Equal(3f,       s.StrokeWidth);
        Assert.Equal(0.8f,     s.Opacity, precision: 5);
        Assert.True(s.Dashed);
        Assert.Equal(45f,      s.Rotation);
    }

    // -- Render - canvas sources -------------------------------------------

    [Fact]
    public void Render_BlankCanvas_ReturnsNonEmptyBase64()
    {
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas     = "blank",
            Background = "white",
            Width      = 100,
            Height     = 80,
            Shapes     = []
        };

        string b64 = AIDrawingCommand.Render(req);

        Assert.False(string.IsNullOrWhiteSpace(b64));
        // Must be valid base-64.
        var bytes = Convert.FromBase64String(b64);
        // PNG magic bytes: 89 50 4E 47
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public void Render_BlackCanvas_ProducesBlackFirstPixel()
    {
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas = "black",
            Width  = 10,
            Height = 10,
            Shapes = []
        };

        string b64  = AIDrawingCommand.Render(req);
        var    bmp  = Base64ToBitmap(b64);
        var    px   = bmp.GetPixel(0, 0);

        Assert.Equal(0,   px.R);
        Assert.Equal(0,   px.G);
        Assert.Equal(0,   px.B);
    }

    [Fact]
    public void Render_WhiteBackground_ProducesWhiteFirstPixel()
    {
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas     = "blank",
            Background = "white",
            Width      = 10,
            Height     = 10,
            Shapes     = []
        };

        string b64 = AIDrawingCommand.Render(req);
        var    bmp = Base64ToBitmap(b64);
        var    px  = bmp.GetPixel(0, 0);

        Assert.Equal(255, px.R);
        Assert.Equal(255, px.G);
        Assert.Equal(255, px.B);
    }

    [Fact]
    public void Render_HexBackground_ResolvedCorrectly()
    {
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas     = "blank",
            Background = "#FF0000",   // red
            Width      = 10,
            Height     = 10,
            Shapes     = []
        };

        string b64 = AIDrawingCommand.Render(req);
        var    bmp = Base64ToBitmap(b64);
        var    px  = bmp.GetPixel(0, 0);

        Assert.Equal(255, px.R);
        Assert.Equal(0,   px.G);
        Assert.Equal(0,   px.B);
    }

    [Fact]
    public void Render_DimensionsClamped_To4096()
    {
        // Width/Height > 4096 should be clamped, not throw.
        var req = new AIDrawingCommand.DrawRequest { Canvas = "blank", Width = 99999, Height = 99999 };
        var ex  = Record.Exception(() => AIDrawingCommand.Render(req));
        Assert.Null(ex);
    }

    // -- Render - shape types ----------------------------------------------

    [Theory]
    [InlineData("rect")]
    [InlineData("ellipse")]
    [InlineData("circle")]
    [InlineData("line")]
    [InlineData("arrow")]
    [InlineData("text")]
    [InlineData("triangle")]
    [InlineData("arc")]
    public void Render_EachShapeType_DoesNotThrow(string type)
    {
        var shape = new AIDrawingCommand.ShapeCommand
        {
            Type        = type,
            X           = 10, Y = 10, X2 = 90, Y2 = 90,
            W           = 80, H = 60, R  = 30,
            Color       = "blue",
            Fill        = true,
            Text        = "Hello",
            Points      = [10, 10, 50, 5, 90, 10, 70, 40, 30, 40],
            StartAngle  = 0,
            SweepAngle  = 120
        };
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas = "blank", Width = 200, Height = 200,
            Shapes = [shape]
        };

        var ex = Record.Exception(() => AIDrawingCommand.Render(req));
        Assert.Null(ex);
    }

    [Fact]
    public void Render_Polygon_WithPoints_DoesNotThrow()
    {
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas = "blank",
            Width  = 200,
            Height = 200,
            Shapes =
            [
                new() { Type = "polygon", Points = [10,10, 100,10, 55,90], Color = "green", Fill = true }
            ]
        };

        var ex = Record.Exception(() => AIDrawingCommand.Render(req));
        Assert.Null(ex);
    }

    [Fact]
    public void Render_FilledRect_PaintsPixelInColor()
    {
        var req = new AIDrawingCommand.DrawRequest
        {
            Canvas = "blank", Background = "white",
            Width  = 100, Height = 100,
            Shapes = [new() { Type = "rect", X = 0, Y = 0, W = 100, H = 100, Color = "#0000FF", Fill = true }]
        };

        var bmp = Base64ToBitmap(AIDrawingCommand.Render(req));
        var px  = bmp.GetPixel(50, 50);

        Assert.Equal(0,   px.R);
        Assert.Equal(0,   px.G);
        Assert.Equal(255, px.B);
    }

    // -- BuildSpaceScene ---------------------------------------------------

    [Fact]
    public void BuildSpaceScene_ReturnsNonEmptyRequest()
    {
        var req = AIDrawingCommand.BuildSpaceScene();
        Assert.Equal("blank",   req.Canvas);
        Assert.Equal(800,       req.Width);
        Assert.Equal(500,       req.Height);
        Assert.NotEmpty(req.Shapes);
    }

    [Fact]
    public void BuildSpaceScene_Renders_WithoutException()
    {
        var req = AIDrawingCommand.BuildSpaceScene();
        var ex  = Record.Exception(() => AIDrawingCommand.Render(req));
        Assert.Null(ex);
    }

    // -- Helper ------------------------------------------------------------

    private static Bitmap Base64ToBitmap(string b64)
    {
        var bytes = Convert.FromBase64String(b64);
        using var ms = new MemoryStream(bytes);
        return new Bitmap(Image.FromStream(ms));
    }
}

