using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="SceneStore"/> covering CRUD operations, disk persistence
/// round-trips, and concurrent access.
///
/// Each test instance gets its own isolated temp directory passed to SceneStore's
/// constructor, so tests run in parallel without interfering with each other or with
/// the user's real data in %LocalAppData%.
/// </summary>
public class SceneStoreTests : IDisposable
{
    private readonly string _scenesDir =
        Path.Combine(Path.GetTempPath(), "ApexTests_Scenes_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_scenesDir))
            try { Directory.Delete(_scenesDir, recursive: true); } catch { /* best-effort */ }
    }

    // -- CreateScene -------------------------------------------------------

    [Fact]
    public void CreateScene_DefaultValues_AreCorrect()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("Test Scene");

        Assert.Equal("Test Scene", scene.Name);
        Assert.Equal(800,   scene.Width);
        Assert.Equal(600,   scene.Height);
        Assert.Equal("white", scene.Background);
        Assert.NotEmpty(scene.Id);
        Assert.Single(scene.Layers);            // one default layer
        Assert.Equal("Layer 1", scene.Layers[0].Name);
    }

    [Fact]
    public void CreateScene_EmptyName_FallsBackToUntitled()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("");
        Assert.Equal("Untitled", scene.Name);
    }

    [Fact]
    public void CreateScene_DimensionsClamped_To8192()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("big", width: 99999, height: -5);
        Assert.Equal(8192, scene.Width);
        Assert.Equal(1,    scene.Height);    // clamped to min=1
    }

    [Fact]
    public void CreateScene_UniqueIds()
    {
        var store  = new SceneStore(_scenesDir);
        var scene1 = store.CreateScene("A");
        var scene2 = store.CreateScene("B");
        Assert.NotEqual(scene1.Id, scene2.Id);
    }

    // -- GetScene ----------------------------------------------------------

    [Fact]
    public void GetScene_ExistingId_ReturnsScene()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("Lookup");
        var found = store.GetScene(scene.Id);
        Assert.NotNull(found);
        Assert.Equal("Lookup", found.Name);
    }

    [Fact]
    public void GetScene_NonExistentId_ReturnsNull()
    {
        var store = new SceneStore(_scenesDir);
        Assert.Null(store.GetScene("does-not-exist"));
    }

    // -- ListScenes --------------------------------------------------------

    [Fact]
    public void ListScenes_OrderedByCreatedAt()
    {
        var store = new SceneStore(_scenesDir);
        var s1 = store.CreateScene("First");
        // Small delay to ensure distinct CreatedAt timestamps.
        Thread.Sleep(10);
        var s2 = store.CreateScene("Second");

        var list = store.ListScenes();
        Assert.Equal(2, list.Length);
        Assert.Equal(s1.Id, list[0].Id);
        Assert.Equal(s2.Id, list[1].Id);
    }

    [Fact]
    public void ListScenes_EmptyStore_ReturnsEmptyArray()
    {
        var store = new SceneStore(_scenesDir);
        Assert.Empty(store.ListScenes());
    }

    // -- UpdateSceneMeta ---------------------------------------------------

    [Fact]
    public void UpdateSceneMeta_UpdatesOnlySuppliedFields()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("Original", width: 400);

        string originalUpdatedAt = scene.UpdatedAt;
        Thread.Sleep(5);

        var updated = store.UpdateSceneMeta(scene.Id, name: "Renamed", width: null, height: null, background: null);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(400, updated.Width);           // unchanged
        Assert.NotEqual(originalUpdatedAt, updated.UpdatedAt);  // Touch() was called
    }

    [Fact]
    public void UpdateSceneMeta_UnknownId_Throws()
    {
        var store = new SceneStore(_scenesDir);
        Assert.Throws<KeyNotFoundException>(() =>
            store.UpdateSceneMeta("ghost", "x", null, null, null));
    }

    // -- DeleteScene -------------------------------------------------------

    [Fact]
    public void DeleteScene_RemovesFromMemoryAndDisk()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("ToDelete");
        string path = Path.Combine(_scenesDir, $"{scene.Id}.json");
        Assert.True(File.Exists(path));

        store.DeleteScene(scene.Id);

        Assert.Null(store.GetScene(scene.Id));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteScene_NonExistentId_IsNoOp()
    {
        var store = new SceneStore(_scenesDir);
        var ex = Record.Exception(() => store.DeleteScene("no-such-id"));
        Assert.Null(ex);
    }

    // -- Layer CRUD --------------------------------------------------------

    [Fact]
    public void AddLayer_AppendsLayerWithIncrementingZIndex()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("LayerTest");

        var layer2 = store.AddLayer(scene.Id, "Layer 2");
        var layer3 = store.AddLayer(scene.Id, "Layer 3");

        var fresh = store.GetScene(scene.Id)!;
        Assert.Equal(3, fresh.Layers.Count);
        Assert.True(layer3.ZIndex > layer2.ZIndex);
    }

    [Fact]
    public void AddLayer_EmptyName_FallsBackToLayer()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("S");
        var layer = store.AddLayer(scene.Id, "");
        Assert.Equal("Layer", layer.Name);
    }

    [Fact]
    public void DeleteLayer_RemovesLayer()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("DL");
        var extra = store.AddLayer(scene.Id, "Extra");

        store.DeleteLayer(scene.Id, extra.Id);

        var fresh = store.GetScene(scene.Id)!;
        Assert.DoesNotContain(fresh.Layers, l => l.Id == extra.Id);
    }

    [Fact]
    public void DeleteLayer_UnknownLayerId_Throws()
    {
        var store = new SceneStore(_scenesDir);
        var scene = store.CreateScene("S");
        Assert.Throws<KeyNotFoundException>(() =>
            store.DeleteLayer(scene.Id, "no-such-layer"));
    }

    // -- Disk persistence round-trip ---------------------------------------

    [Fact]
    public void CreateScene_PersistsToDisk_NewStoreLoadsIt()
    {
        // Write via first instance.
        var store1 = new SceneStore(_scenesDir);
        var scene  = store1.CreateScene("Persisted", width: 1024, height: 768, background: "navy");

        // Load from disk via a second instance.
        var store2 = new SceneStore(_scenesDir);
        var loaded = store2.GetScene(scene.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Persisted", loaded.Name);
        Assert.Equal(1024,   loaded.Width);
        Assert.Equal(768,    loaded.Height);
        Assert.Equal("navy", loaded.Background);
    }

    [Fact]
    public void DeleteScene_DiskFileRemovedBeforeReload()
    {
        var store1 = new SceneStore(_scenesDir);
        var scene  = store1.CreateScene("Ephemeral");
        store1.DeleteScene(scene.Id);

        var store2 = new SceneStore(_scenesDir);
        Assert.Null(store2.GetScene(scene.Id));
    }

    // -- Concurrent access -------------------------------------------------

    [Fact]
    public async Task CreateScene_ConcurrentCreates_AllSucceed()
    {
        var store = new SceneStore(_scenesDir);
        const int count = 20;
        await Task.WhenAll(Enumerable.Range(0, count)
            .Select(i => Task.Run(() => store.CreateScene($"Concurrent_{i}"))));

        var list = store.ListScenes();
        Assert.Equal(count, list.Length);
        // All IDs must be distinct.
        Assert.Equal(count, list.Select(s => s.Id).Distinct().Count());
    }
}

