using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="Scene"/>, <see cref="Layer"/>, <see cref="SceneShape"/>,
/// and the <see cref="SceneIds"/> factory.
/// All tests are pure-object - no disk I/O, no GDI+.
/// </summary>
public class SceneModelTests
{
    // -- Scene.Touch -------------------------------------------------------

    [Fact]
    public void Touch_UpdatesUpdatedAt()
    {
        var scene = new Scene { UpdatedAt = "2020-01-01T00:00:00.0000000+00:00" };
        Thread.Sleep(5);
        scene.Touch();
        Assert.NotEqual("2020-01-01T00:00:00.0000000+00:00", scene.UpdatedAt);
    }

    // -- Scene.FlattenForRender --------------------------------------------

    [Fact]
    public void FlattenForRender_NoLayers_ReturnsEmpty()
    {
        var scene = new Scene { Layers = [] };
        Assert.Empty(scene.FlattenForRender());
    }

    [Fact]
    public void FlattenForRender_HiddenLayer_Excluded()
    {
        var scene = new Scene
        {
            Layers =
            [
                new Layer
                {
                    Visible = false,
                    Shapes  = [new SceneShape { Visible = true, Shape = new() { Type = "rect" } }]
                }
            ]
        };
        Assert.Empty(scene.FlattenForRender());
    }

    [Fact]
    public void FlattenForRender_HiddenShape_Excluded()
    {
        var scene = new Scene
        {
            Layers =
            [
                new Layer
                {
                    Visible = true,
                    Shapes  = [new SceneShape { Visible = false, Shape = new() { Type = "rect" } }]
                }
            ]
        };
        Assert.Empty(scene.FlattenForRender());
    }

    [Fact]
    public void FlattenForRender_VisibleShapes_Included()
    {
        var scene = new Scene
        {
            Layers =
            [
                new Layer
                {
                    Visible = true,
                    Shapes  =
                    [
                        new SceneShape { Visible = true, Shape = new() { Type = "rect"   } },
                        new SceneShape { Visible = true, Shape = new() { Type = "circle" } }
                    ]
                }
            ]
        };
        var result = scene.FlattenForRender();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FlattenForRender_LayerZIndexOrder_AscendingByDefault()
    {
        var scene = new Scene
        {
            Layers =
            [
                new Layer { ZIndex = 10, Visible = true,
                    Shapes = [new SceneShape { Visible = true, Shape = new() { Type = "A", Color = "red"  } }] },
                new Layer { ZIndex = 1,  Visible = true,
                    Shapes = [new SceneShape { Visible = true, Shape = new() { Type = "B", Color = "blue" } }] }
            ]
        };
        var result = scene.FlattenForRender();
        // Layer with ZIndex=1 comes first (lower = rendered first).
        Assert.Equal("blue", result[0].Color);
        Assert.Equal("red",  result[1].Color);
    }

    [Fact]
    public void FlattenForRender_OpacityMultiplied_ByLayerOpacity()
    {
        var scene = new Scene
        {
            Layers =
            [
                new Layer
                {
                    Visible = true,
                    Opacity = 0.5f,
                    Shapes  = [new SceneShape { Visible = true, Shape = new() { Type = "rect", Opacity = 0.8f } }]
                }
            ]
        };
        var result = scene.FlattenForRender();
        Assert.Equal(0.4f, result[0].Opacity, precision: 5);  // 0.5 * 0.8
    }

    [Fact]
    public void FlattenForRender_ShapeIsClone_MutationDoesNotAffectScene()
    {
        var original = new AIDrawingCommand.ShapeCommand { Type = "rect", Color = "green" };
        var scene = new Scene
        {
            Layers =
            [
                new Layer
                {
                    Visible = true,
                    Shapes  = [new SceneShape { Visible = true, Shape = original }]
                }
            ]
        };

        var clones = scene.FlattenForRender();
        clones[0].Color = "mutated";

        // Original shape in scene must be untouched.
        Assert.Equal("green", scene.Layers[0].Shapes[0].Shape.Color);
    }

    // -- Scene.ToDrawRequest -----------------------------------------------

    [Fact]
    public void ToDrawRequest_UsesSceneDimensions()
    {
        var scene = new Scene { Width = 1280, Height = 720, Background = "navy" };
        var req   = scene.ToDrawRequest();
        Assert.Equal(1280,   req.Width);
        Assert.Equal(720,    req.Height);
        Assert.Equal("navy", req.Background);
        Assert.Equal("blank", req.Canvas);
    }

    // -- SceneIds ----------------------------------------------------------

    [Fact]
    public void SceneIds_New_ReturnsEightCharString()
    {
        string id = SceneIds.New();
        Assert.Equal(8, id.Length);
    }

    [Fact]
    public void SceneIds_New_ReturnsDifferentValues()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => SceneIds.New()).ToHashSet();
        // Very unlikely to get fewer than 50 unique IDs from 50 calls.
        Assert.Equal(50, ids.Count);
    }

    // -- Layer defaults ----------------------------------------------------

    [Fact]
    public void Layer_DefaultOpacity_IsOne()
    {
        var l = new Layer();
        Assert.Equal(1.0f, l.Opacity);
    }

    [Fact]
    public void Layer_DefaultVisible_IsTrue()
    {
        var l = new Layer();
        Assert.True(l.Visible);
    }

    // -- SceneShape defaults -----------------------------------------------

    [Fact]
    public void SceneShape_DefaultVisible_IsTrue()
    {
        var ss = new SceneShape();
        Assert.True(ss.Visible);
    }

    [Fact]
    public void SceneShape_DefaultLocked_IsFalse()
    {
        var ss = new SceneShape();
        Assert.False(ss.Locked);
    }
}

