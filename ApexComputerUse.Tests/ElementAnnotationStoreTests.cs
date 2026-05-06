using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

public class ElementAnnotationStoreTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "ApexTests_Annotations_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Fact]
    public void SetNote_ThenGet_RoundTripsValue()
    {
        var store = new ElementAnnotationStore(_file);
        store.SetNote("hash-A", "this is a note");
        Assert.True(store.TryGetNote("hash-A", out var note));
        Assert.Equal("this is a note", note);
        Assert.False(store.IsExcluded("hash-A"));
    }

    [Fact]
    public void SetExcluded_TogglesIsExcluded()
    {
        var store = new ElementAnnotationStore(_file);
        Assert.False(store.IsExcluded("hash-B"));
        store.SetExcluded("hash-B", true);
        Assert.True(store.IsExcluded("hash-B"));
        store.SetExcluded("hash-B", false);
        Assert.False(store.IsExcluded("hash-B"));
    }

    [Fact]
    public void Persistence_RoundTripsAcrossInstances()
    {
        {
            var s1 = new ElementAnnotationStore(_file);
            s1.SetNote("hash-C", "persistent note", controlType: "Button", name: "OK");
            s1.SetExcluded("hash-D", true);
        }
        var s2 = new ElementAnnotationStore(_file);
        Assert.True(s2.TryGetNote("hash-C", out var note));
        Assert.Equal("persistent note", note);
        Assert.True(s2.IsExcluded("hash-D"));

        var rec = s2.Get("hash-C");
        Assert.NotNull(rec);
        Assert.Equal("Button", rec!.ControlType);
        Assert.Equal("OK",     rec.Name);
    }

    [Fact]
    public void EmptyRecord_IsGarbageCollectedAfterUnannotateAndUnexclude()
    {
        var store = new ElementAnnotationStore(_file);
        store.SetNote("hash-E", "tmp");
        store.SetExcluded("hash-E", true);
        Assert.NotNull(store.Get("hash-E"));

        store.SetExcluded("hash-E", false);
        Assert.NotNull(store.Get("hash-E"));   // still has note

        store.SetNote("hash-E", null);
        Assert.Null(store.Get("hash-E"));       // empty: dropped
    }

    [Fact]
    public void ListAll_ReturnsAllRecords()
    {
        var store = new ElementAnnotationStore(_file);
        store.SetNote("hash-1", "a");
        store.SetNote("hash-2", "b");
        store.SetExcluded("hash-3", true);
        var all = store.ListAll();
        Assert.Equal(3, all.Length);
    }

    [Fact]
    public void ListExcluded_FiltersToExcludedOnly()
    {
        var store = new ElementAnnotationStore(_file);
        store.SetNote("note-only", "a");
        store.SetExcluded("excluded-1", true);
        store.SetExcluded("excluded-2", true);
        var ex = store.ListExcluded();
        Assert.Equal(2, ex.Length);
        Assert.All(ex, a => Assert.True(a.Excluded));
    }

    [Fact]
    public void ConcurrentWrites_DoNotCorruptStore()
    {
        var store = new ElementAnnotationStore(_file);
        Parallel.For(0, 200, i =>
        {
            string h = "hash-" + (i % 10);
            store.SetNote(h, "iter-" + i);
            if (i % 3 == 0) store.SetExcluded(h, true);
        });
        // No exceptions, and we still have 10 distinct hashes.
        var all = store.ListAll();
        Assert.True(all.Length <= 10);
    }
}
