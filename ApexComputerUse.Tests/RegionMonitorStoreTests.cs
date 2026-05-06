using System.Drawing;
using System.Drawing.Imaging;
using ApexComputerUse;
using Xunit;

public class RegionMonitorStoreTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "apex-mon-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));

    [Fact]
    public void Create_AssignsIdAndPersistsToDisk()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            var m = store.Create(new RegionMonitor
            {
                Name = "leds",
                Regions = new() { new MonitorRegion { X = 10, Y = 20, Width = 50, Height = 50 } }
            });

            Assert.False(string.IsNullOrEmpty(m.Id));
            Assert.True(File.Exists(Path.Combine(dir, m.Id + ".json")));

            var reloaded = new RegionMonitorStore(dir).Get(m.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("leds", reloaded!.Name);
            Assert.Single(reloaded.Regions);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Create_ClampsIntervalAndDefaultsApplied()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            var m = store.Create(new RegionMonitor { IntervalMs = 10 }); // below floor
            Assert.Equal(100, m.IntervalMs);
            Assert.Equal(5.0, m.ThresholdPct);   // default
            Assert.Equal(8,   m.Tolerance);      // default
            Assert.False(m.Enabled);             // default
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Update_PartialFieldsLeavesOthersUntouched()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            var m = store.Create(new RegionMonitor { Name = "before", IntervalMs = 1000, ThresholdPct = 5.0 });
            var u = store.Update(m.Id, name: "after");
            Assert.Equal("after", u.Name);
            Assert.Equal(1000,    u.IntervalMs);
            Assert.Equal(5.0,     u.ThresholdPct);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Update_UnknownIdThrowsKeyNotFound()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            Assert.Throws<KeyNotFoundException>(() => store.Update("nope", name: "x"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void RecordFire_UpdatesTelemetryAndIncrementsCount()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            var m = store.Create(new RegionMonitor { Name = "x" });
            Assert.Equal(0, m.HitCount);

            store.RecordFire(m.Id, regionIndex: 2, percentDiff: 12.5);
            store.RecordFire(m.Id, regionIndex: 0, percentDiff: 6.0);

            var reloaded = store.Get(m.Id)!;
            Assert.Equal(2, reloaded.HitCount);
            Assert.Equal(0, reloaded.LastRegionIndex);   // most recent
            Assert.Equal(6.0, reloaded.LastPercentDiff!.Value, 3);
            Assert.NotNull(reloaded.LastFiredUtc);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Delete_RemovesFromMemoryAndDisk()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            var m = store.Create(new RegionMonitor());
            string path = Path.Combine(dir, m.Id + ".json");
            Assert.True(File.Exists(path));

            Assert.True(store.Delete(m.Id));
            Assert.Null(store.Get(m.Id));
            Assert.False(File.Exists(path));
            Assert.False(store.Delete(m.Id));   // second time = false
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void List_ReturnsAllStoredMonitors()
    {
        string dir = TempDir();
        try
        {
            var store = new RegionMonitorStore(dir);
            store.Create(new RegionMonitor { Name = "a" });
            store.Create(new RegionMonitor { Name = "b" });
            store.Create(new RegionMonitor { Name = "c" });
            Assert.Equal(3, store.List().Length);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void DiffPercent_IdenticalBitmapsZero()
    {
        using var a = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var b = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        FillSolid(a, Color.Red);
        FillSolid(b, Color.Red);
        Assert.Equal(0.0, RegionMonitorRunner.DiffPercent(a, b, tolerance: 0));
    }

    [Fact]
    public void DiffPercent_TotallyDifferentBitmapsHundredPercent()
    {
        using var a = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var b = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        FillSolid(a, Color.Red);
        FillSolid(b, Color.Blue);
        Assert.Equal(100.0, RegionMonitorRunner.DiffPercent(a, b, tolerance: 0), 1);
    }

    [Fact]
    public void DiffPercent_ToleranceFiltersTinyDiffs()
    {
        using var a = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var b = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        FillSolid(a, Color.FromArgb(255, 100, 100, 100));
        FillSolid(b, Color.FromArgb(255, 105, 105, 105));   // 5/255 channel diff

        // Tolerance 10 → all pixels considered unchanged.
        Assert.Equal(0.0, RegionMonitorRunner.DiffPercent(a, b, tolerance: 10), 1);

        // Tolerance 0 → all pixels considered changed (any diff > 0 counts).
        Assert.Equal(100.0, RegionMonitorRunner.DiffPercent(a, b, tolerance: 0), 1);
    }

    [Fact]
    public void DiffPercent_PartialChangeIsApproximate()
    {
        // Half the bitmap differs => ~50% diff.
        using var a = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var b = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        FillSolid(a, Color.Red);
        FillSolid(b, Color.Red);
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 10; x++)
                b.SetPixel(x, y, Color.Blue);
        double pct = RegionMonitorRunner.DiffPercent(a, b, tolerance: 0);
        Assert.InRange(pct, 49.0, 51.0);
    }

    private static void FillSolid(Bitmap bmp, Color c)
    {
        using var g = Graphics.FromImage(bmp);
        g.Clear(c);
    }
}
