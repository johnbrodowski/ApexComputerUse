using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

public class RegionMapStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "ApexTests_RegionMaps_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static RegionMap Sample() => new()
    {
        Name        = "Checkers",
        WindowTitle = "Checkers",
        OriginX     = 100,
        OriginY     = 200,
        CellWidth   = 60,
        CellHeight  = 60,
        Rows        = 8,
        Cols        = 8,
        Color       = "#33FF33"
    };

    [Fact]
    public void Create_AssignsIdAndPersists()
    {
        var store = new RegionMapStore(_dir);
        var m = store.Create(Sample());
        Assert.False(string.IsNullOrEmpty(m.Id));
        Assert.True(File.Exists(Path.Combine(_dir, m.Id + ".json")));
    }

    [Fact]
    public void GetAndList_ReturnCreatedMap()
    {
        var store = new RegionMapStore(_dir);
        var m = store.Create(Sample());
        var fetched = store.Get(m.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Checkers", fetched!.Name);
        Assert.Single(store.List());
    }

    [Fact]
    public void Persistence_RoundTripsAcrossInstances()
    {
        string id;
        {
            var s1 = new RegionMapStore(_dir);
            id = s1.Create(Sample()).Id;
        }
        var s2 = new RegionMapStore(_dir);
        var m = s2.Get(id);
        Assert.NotNull(m);
        Assert.Equal(8, m!.Rows);
        Assert.Equal("Checkers", m.WindowTitle);
    }

    [Fact]
    public void Update_ChangesGeometryButNotId()
    {
        var store = new RegionMapStore(_dir);
        var m = store.Create(Sample());
        var u = store.Update(m.Id, name: null, windowTitle: null, elementHash: null,
                              originX: 250, originY: 300, cellWidth: 80, cellHeight: 80,
                              rows: 6, cols: 6, color: null, notes: "tweaked", labels: null);
        Assert.Equal(m.Id, u.Id);
        Assert.Equal(250, u.OriginX);
        Assert.Equal(80,  u.CellWidth);
        Assert.Equal(6,   u.Rows);
        Assert.Equal("tweaked", u.Notes);
    }

    [Fact]
    public void Delete_RemovesMapAndFile()
    {
        var store = new RegionMapStore(_dir);
        var m = store.Create(Sample());
        store.Delete(m.Id);
        Assert.Null(store.Get(m.Id));
        Assert.False(File.Exists(Path.Combine(_dir, m.Id + ".json")));
    }

    [Fact]
    public void FindByScope_FiltersByWindowTitle()
    {
        var store = new RegionMapStore(_dir);
        store.Create(new RegionMap { Name = "A", WindowTitle = "Checkers", Rows = 8, Cols = 8 });
        store.Create(new RegionMap { Name = "B", WindowTitle = "Solitaire", Rows = 8, Cols = 8 });
        var hits = store.FindByScope("Checkers", null);
        Assert.Single(hits);
        Assert.Equal("A", hits[0].Name);
    }

    [Fact]
    public void CellToPixel_ReturnsCenterOfCell()
    {
        // Origin (100,200), 60x60 cells. Cell (0,0) center = (130, 230). Cell (1,2) center = (250, 290).
        var m = Sample();
        var (x0, y0) = RegionMapStore.CellToPixel(m, 0, 0);
        Assert.Equal(130, x0); Assert.Equal(230, y0);
        var (x1, y1) = RegionMapStore.CellToPixel(m, 1, 2);
        Assert.Equal(250, x1); Assert.Equal(290, y1);
    }

    [Fact]
    public void CellToPixel_OutOfRange_Throws()
    {
        var m = Sample();
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionMapStore.CellToPixel(m, 8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionMapStore.CellToPixel(m, 0, -1));
    }

    [Fact]
    public void BuildGridDrawRequest_HasExpectedLineCount()
    {
        var m = Sample();
        var req = RegionMapStore.BuildGridDrawRequest(m, canvas: "blank", labelCells: false);
        // 8 rows -> 9 horizontal lines; 8 cols -> 9 vertical lines = 18
        int lines = req.Shapes.Count(s => s.Type == "line");
        Assert.Equal(18, lines);
    }

    [Fact]
    public void BuildGridDrawRequest_LabelCells_ProducesPerCellLabels()
    {
        var m = Sample();
        var req = RegionMapStore.BuildGridDrawRequest(m, canvas: "blank", labelCells: true);
        int labels = req.Shapes.Count(s => s.Type == "text");
        Assert.Equal(8 * 8, labels);
    }
}
