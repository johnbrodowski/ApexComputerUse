using System.Collections.Generic;
using System.Text.Json;
using ApexComputerUse;
using Xunit;

using ElementNode = ApexComputerUse.CommandProcessor.ElementNode; // internal via InternalsVisibleTo

namespace ApexComputerUse.Tests;

/// <summary>
/// Tree-shape post-processor tests. These verify the pure-logic pieces of the
/// browser-friendly /elements features \- <c>FilterTreeByMatch</c> and
/// <c>CollapseSingleChildChains</c> \- and that opt-in node fields round-trip
/// through JSON serialisation intact. No live UI / FlaUI / UIA involvement.
/// </summary>
public class CommandProcessorTreeTests
{
    // \-\- Helpers \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    private static ElementNode Node(
        int id, string controlType, string name = "", string automationId = "",
        IEnumerable<ElementNode>? children = null,
        int? childCount = null, int? descendantCount = null,
        string? path = null, string? value = null)
        => new()
        {
            Id              = id,
            ControlType     = controlType,
            Name            = name,
            AutomationId    = automationId,
            Children        = children == null ? null : new List<ElementNode>(children),
            ChildCount      = childCount,
            DescendantCount = descendantCount,
            Path            = path,
            Value           = value
        };

    private static List<ElementNode> Flatten(ElementNode? root)
    {
        var all = new List<ElementNode>();
        Walk(root);
        return all;

        void Walk(ElementNode? n)
        {
            if (n == null) return;
            all.Add(n);
            if (n.Children != null)
                foreach (var c in n.Children) Walk(c);
        }
    }

    // \-\- MatchFilter \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    [Fact]
    public void MatchFilter_KeepsOnlyBranchesContainingMatches()
    {
        // Tree:
        //   Window
        //   \+\-\- Button "Save"
        //   \+\-\- Pane
        //   \|   \+\-\- Button "Add to Cart"   \? match
        //   \+\-\- Button "Cancel"
        var tree = Node(1, "Window", name: "App", children: new[]
        {
            Node(2, "Button", name: "Save"),
            Node(3, "Pane", children: new[]
            {
                Node(4, "Button", name: "Add to Cart")
            }),
            Node(5, "Button", name: "Cancel")
        });

        var filtered = CommandProcessor.FilterTreeByMatch(tree, "add to cart", isRoot: true);
        Assert.NotNull(filtered);

        var ids = Flatten(filtered).ConvertAll(n => n.Id);
        // Window (root) kept as skeleton, path Pane \-> Button kept, siblings Save/Cancel pruned.
        Assert.Contains(1, ids);
        Assert.Contains(3, ids);
        Assert.Contains(4, ids);
        Assert.DoesNotContain(2, ids); // "Save" \- unrelated sibling
        Assert.DoesNotContain(5, ids); // "Cancel" \- unrelated sibling
    }

    [Fact]
    public void MatchFilter_IsCaseInsensitive_AndSearchesAutomationIdAndValue()
    {
        var tree = Node(1, "Window", children: new[]
        {
            Node(2, "Edit", automationId: "emailInput", value: "alice@example.com"),
            Node(3, "Button", name: "Submit")
        });

        // AutomationId match, different casing.
        var byAutoId = CommandProcessor.FilterTreeByMatch(tree, "EMAILINPUT", isRoot: true);
        var ids1 = Flatten(byAutoId).ConvertAll(n => n.Id);
        Assert.Contains(2, ids1);
        Assert.DoesNotContain(3, ids1);

        // Value substring match \- picks up the filled-in email.
        var byValue = CommandProcessor.FilterTreeByMatch(tree, "alice@", isRoot: true);
        var ids2 = Flatten(byValue).ConvertAll(n => n.Id);
        Assert.Contains(2, ids2);
    }

    // \-\- CollapseSingleChildChains \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    [Fact]
    public void CollapseChains_SkipsIdentitylessPaneGroupCustom_ButPreservesNamedContainers()
    {
        // Tree:
        //   Window
        //   \+\-\- Pane (no name, no id)        \? collapsible
        //       \+\-\- Group (no name, no id)   \? collapsible
        //           \+\-\- Custom (no name, no id) \? collapsible
        //               \+\-\- Pane  name="Main" \? IDENTIFIED \- must NOT collapse
        //                   \+\-\- Button "Go"
        var tree = Node(1, "Window", name: "App", children: new[]
        {
            Node(2, "Pane", children: new[]
            {
                Node(3, "Group", children: new[]
                {
                    Node(4, "Custom", children: new[]
                    {
                        Node(5, "Pane", name: "Main", children: new[]
                        {
                            Node(6, "Button", name: "Go")
                        })
                    })
                })
            })
        });

        var collapsed = CommandProcessor.CollapseSingleChildChains(tree);
        Assert.NotNull(collapsed);
        var ids = Flatten(collapsed).ConvertAll(n => n.Id);

        Assert.Contains(1, ids); // root kept
        Assert.DoesNotContain(2, ids); // identity-less Pane skipped
        Assert.DoesNotContain(3, ids); // identity-less Group skipped
        Assert.DoesNotContain(4, ids); // identity-less Custom skipped
        Assert.Contains(5, ids); // NAMED Pane "Main" kept \- has identity
        Assert.Contains(6, ids); // leaf Button kept

        // Button's ID is unchanged (stability).
        var go = Flatten(collapsed).Find(n => n.Id == 6);
        Assert.NotNull(go);
        Assert.Equal("Button", go!.ControlType);
        Assert.Equal("Go", go.Name);
    }

    [Fact]
    public void CollapseChains_DoesNotCollapse_WhenParentHasMultipleChildren()
    {
        // Two children \-> parent must survive even if identity-less, because
        // "1-in-1-in-1" is the only shape we target.
        var tree = Node(1, "Window", name: "App", children: new[]
        {
            Node(2, "Pane", children: new[]
            {
                Node(3, "Button", name: "A"),
                Node(4, "Button", name: "B")
            })
        });

        var collapsed = CommandProcessor.CollapseSingleChildChains(tree);
        var ids = Flatten(collapsed).ConvertAll(n => n.Id);
        Assert.Contains(2, ids); // Pane retained because it has two children
        Assert.Contains(3, ids);
        Assert.Contains(4, ids);
    }

    [Fact]
    public void CollapseChains_PreservesElementIdsThroughCollapse()
    {
        // The hoisted descendant's ID must equal its original ID so follow-up
        // /elements?id=<id> and /execute id=<id> calls still resolve.
        var tree = Node(1, "Window", name: "App", children: new[]
        {
            Node(2, "Pane", children: new[]
            {
                Node(99, "Button", name: "ClickMe", automationId: "btnClick")
            })
        });

        var collapsed = CommandProcessor.CollapseSingleChildChains(tree);
        var btn = Flatten(collapsed).Find(n => n.Id == 99);
        Assert.NotNull(btn);
        Assert.Equal("btnClick", btn!.AutomationId);
    }

    // \-\- ElementNode JSON shape \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    [Fact]
    public void ElementNode_OptionalFields_AreOmittedWhenNull_AndIncludedWhenPopulated()
    {
        // ChildCount + DescendantCount on a truncated node are the primary
        // new emission \- verify they survive serialisation.
        var truncated = Node(42, "Pane", name: "Outer",
            childCount: 3, descendantCount: 17,
            path: "Window > Pane",
            value: "hello");

        var json = JsonSerializer.Serialize(truncated, new JsonSerializerOptions
        {
            WriteIndented          = true,
            PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(42, root.GetProperty("id").GetInt32());
        Assert.Equal(3,  root.GetProperty("childCount").GetInt32());
        Assert.Equal(17, root.GetProperty("descendantCount").GetInt32());
        Assert.Equal("Window > Pane", root.GetProperty("path").GetString());
        Assert.Equal("hello", root.GetProperty("value").GetString());

        // Null fields must be absent (would inflate every emitted element otherwise).
        Assert.False(root.TryGetProperty("helpText", out _));
        Assert.False(root.TryGetProperty("children", out _));
    }
}
