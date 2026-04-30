using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace ApexComputerUse
{
    public partial class ApexHelper
    {
        // -- Screenshot ----------------------------------------------------

        public string CaptureElement(AutomationElement el, string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using var img = Capture.Element(el);
            img.ToFile(path);
            return path;
        }

        public string CaptureScreen(string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using var img = Capture.Screen();
            img.ToFile(path);
            return path;
        }

        // -- Capture to base64 ---------------------------------------------

        public string CaptureElementToBase64(AutomationElement el)
        {
            using var img = Capture.Element(el);
            return BitmapToBase64(img.Bitmap);
        }

        public string CaptureScreenToBase64()
        {
            using var img = Capture.Screen();
            return BitmapToBase64(img.Bitmap);
        }

        public string StitchElementsToBase64(IList<AutomationElement> elements)
        {
            var captures = elements.Select(e => Capture.Element(e)).ToList();
            try
            {
                const int gap = 4;
                int width  = captures.Max(c => c.Bitmap.Width);
                int height = captures.Sum(c => c.Bitmap.Height) + gap * (captures.Count - 1);

                using var canvas = new System.Drawing.Bitmap(width, Math.Max(height, 1));
                using var g      = System.Drawing.Graphics.FromImage(canvas);
                g.Clear(System.Drawing.Color.FromArgb(40, 40, 40));

                int y = 0;
                foreach (var cap in captures)
                {
                    g.DrawImage(cap.Bitmap, 0, y);
                    y += cap.Bitmap.Height + gap;
                }
                return BitmapToBase64(canvas);
            }
            finally { foreach (var c in captures) c.Dispose(); }
        }

        private static string BitmapToBase64(System.Drawing.Bitmap bmp)
        {
            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

    }
}

