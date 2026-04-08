using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using System.Drawing;
using SysImaging = System.Drawing.Imaging;
using Tesseract;

namespace ApexComputerUse
{
    /// <summary>
    /// Captures a FlaUI element and runs Tesseract OCR on it.
    ///
    /// Requirements:
    ///   - tessdata folder containing language files (e.g. eng.traineddata).
    ///   - Default tessdata path: &lt;exe folder&gt;\tessdata
    ///   - Download from: https://github.com/tesseract-ocr/tessdata
    /// </summary>
    public class OcrHelper : IDisposable
    {
        private readonly TesseractEngine _engine;

        public string TessDataPath { get; }
        public string Language { get; }

        public OcrHelper(string? tessDataPath = null, string language = "eng")
        {
            TessDataPath = tessDataPath
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

            Language = language;

            if (!Directory.Exists(TessDataPath))
                throw new DirectoryNotFoundException(
                    $"tessdata folder not found at: {TessDataPath}\n" +
                    $"Download '{language}.traineddata' from https://github.com/tesseract-ocr/tessdata " +
                    $"and place it in that folder.");

            _engine = new TesseractEngine(TessDataPath, language, EngineMode.Default);
        }

        // ── Core OCR ──────────────────────────────────────────────────────

        /// <summary>Captures the element and returns the recognised text.</summary>
        public OcrResult OcrElement(AutomationElement element)
        {
            using var capture = Capture.Element(element);
            using var bitmap  = CaptureToBitmap(capture);
            return RunOcr(bitmap);
        }

        /// <summary>Captures a sub-rectangle of the element and returns the recognised text.</summary>
        public OcrResult OcrElementRegion(AutomationElement element, Rectangle region)
        {
            using var capture = Capture.Element(element);
            using var full    = CaptureToBitmap(capture);
            using var cropped = CropBitmap(full, region);
            return RunOcr(cropped);
        }

        /// <summary>Runs OCR on an existing image file.</summary>
        public OcrResult OcrFile(string imagePath)
        {
            using var bitmap = new Bitmap(imagePath);
            return RunOcr(bitmap);
        }

        // ── Capture + save + OCR ──────────────────────────────────────────

        /// <summary>
        /// Captures the element, saves the image to disk, runs OCR, and returns the result.
        /// Useful for debugging — you can open the saved image to see exactly what was fed to Tesseract.
        /// </summary>
        public OcrResult OcrElementAndSave(AutomationElement element, string saveFolder)
        {
            Directory.CreateDirectory(saveFolder);
            var imagePath = Path.Combine(saveFolder, $"ocr_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            using var capture = Capture.Element(element);
            using var bitmap  = CaptureToBitmap(capture);
            bitmap.Save(imagePath, SysImaging.ImageFormat.Png);

            var result = RunOcr(bitmap);
            result.SavedImagePath = imagePath;
            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private OcrResult RunOcr(Bitmap bitmap)
        {
            using var pix  = BitmapToPix(bitmap);
            using var page = _engine.Process(pix);

            return new OcrResult
            {
                Text       = page.GetText().Trim(),
                Confidence = page.GetMeanConfidence(),
                Language   = Language
            };
        }

        private static Bitmap CaptureToBitmap(CaptureImage capture)
        {
            // CaptureImage exposes a Bitmap property
            return new Bitmap(capture.Bitmap);
        }

        private static Bitmap CropBitmap(Bitmap source, Rectangle region)
        {
            var safe = Rectangle.Intersect(new Rectangle(0, 0, source.Width, source.Height), region);
            return source.Clone(safe, source.PixelFormat);
        }

        private static Pix BitmapToPix(Bitmap bitmap)
        {
            // Save to a MemoryStream then load via Tesseract — avoids temp files
            using var ms = new MemoryStream();
            bitmap.Save(ms, SysImaging.ImageFormat.Png);
            ms.Position = 0;
            return Pix.LoadFromMemory(ms.ToArray());
        }

        public void Dispose() => _engine.Dispose();
    }

    public class OcrResult
    {
        public string Text        { get; set; } = "";
        public float  Confidence  { get; set; }
        public string Language    { get; set; } = "";
        public string? SavedImagePath { get; set; }

        public override string ToString() =>
            $"Confidence: {Confidence:P1}\n{Text}";
    }
}
