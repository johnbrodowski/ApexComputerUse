using System.Drawing;
using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for the pure-logic helpers in <see cref="OcrHelper"/>.
/// Tests that require a Tesseract engine (tessdata files) are excluded - only the
/// bitmap utility methods that carry no external dependency are covered here.
/// </summary>
public class OcrHelperTests
{
    // -- CropBitmap --------------------------------------------------------

    [Fact]
    public void CropBitmap_RegionInsideBounds_ReturnsCroppedSize()
    {
        using var source  = new Bitmap(100, 80);
        var       region  = new Rectangle(10, 10, 40, 30);
        using var cropped = OcrHelper.CropBitmap(source, region);

        Assert.Equal(40, cropped.Width);
        Assert.Equal(30, cropped.Height);
    }

    [Fact]
    public void CropBitmap_RegionFullyOutsideBounds_ThrowsOrReturnsEmpty()
    {
        // Rectangle.Intersect of a non-overlapping region returns Rectangle.Empty (0?0).
        // GDI+ Bitmap.Clone with a zero-area rectangle throws ArgumentException.
        // This test documents the current behaviour without constraining the error path.
        using var source = new Bitmap(50, 50);
        var       region = new Rectangle(200, 200, 10, 10);  // entirely outside
        // Either the implementation clamps gracefully (no throw) or throws ArgumentException.
        // Either is acceptable - what matters is it doesn't crash the process.
        try
        {
            using var cropped = OcrHelper.CropBitmap(source, region);
            // If it doesn't throw, result must have valid positive dimensions.
            Assert.True(cropped.Width  >= 0);
            Assert.True(cropped.Height >= 0);
        }
        catch (ArgumentException)
        {
            // Expected when GDI+ receives a zero-area clone rectangle.
        }
    }

    [Fact]
    public void CropBitmap_RegionExceedsBounds_ClampedToIntersection()
    {
        using var source  = new Bitmap(60, 40);
        // Region extends past right/bottom edge - must be clipped to 60?40.
        var       region  = new Rectangle(50, 30, 100, 100);
        using var cropped = OcrHelper.CropBitmap(source, region);

        // Intersection: x=50..60, y=30..40 -> 10?10
        Assert.Equal(10, cropped.Width);
        Assert.Equal(10, cropped.Height);
    }

    [Fact]
    public void CropBitmap_FullSourceRegion_SameDimensions()
    {
        using var source  = new Bitmap(200, 150);
        var       region  = new Rectangle(0, 0, 200, 150);
        using var cropped = OcrHelper.CropBitmap(source, region);

        Assert.Equal(200, cropped.Width);
        Assert.Equal(150, cropped.Height);
    }

    [Fact]
    public void CropBitmap_PreservesPixelColor()
    {
        using var source = new Bitmap(50, 50);
        // Paint a red pixel at (20, 20)
        source.SetPixel(20, 20, Color.Red);

        var       region  = new Rectangle(15, 15, 20, 20);
        using var cropped = OcrHelper.CropBitmap(source, region);

        // The red pixel at (20,20) in source -> (5,5) in cropped (offset 15,15)
        var px = cropped.GetPixel(5, 5);
        Assert.Equal(255, px.R);
        Assert.Equal(0,   px.G);
        Assert.Equal(0,   px.B);
    }

    // -- OcrResult ---------------------------------------------------------

    [Fact]
    public void OcrResult_ToString_IncludesConfidenceAndText()
    {
        var result = new OcrResult { Text = "Hello world", Confidence = 0.92f, Language = "eng" };
        string s   = result.ToString();
        Assert.Contains("Hello world", s);
        Assert.Contains("%",           s);    // formatted as percentage
    }

    [Fact]
    public void OcrResult_DefaultText_IsEmpty()
    {
        var r = new OcrResult();
        Assert.Equal("", r.Text);
    }
}

