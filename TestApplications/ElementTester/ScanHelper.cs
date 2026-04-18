namespace ApexUIBridge.ElementTester;

using System.Text.RegularExpressions;

/// <summary>
/// Helpers for parsing SCAN_WINDOW output and finding elements by name, AutomationId, or type.
/// </summary>
public static class ScanHelper
{
    /// <summary>Parsed element from a scan line.</summary>
    public sealed record ScannedElement(long Id, string RawLine, string? Name, string? AutomationId, string? ControlType);

    /// <summary>Parse all elements from SCAN_WINDOW data.</summary>
    public static List<ScannedElement> ParseAll(string scanData)
    {
        var list = new List<ScannedElement>();
        foreach (var line in scanData.Split('\n'))
        {
            var idMatch = Regex.Match(line, @"ID:(\d+)");
            if (!idMatch.Success || !long.TryParse(idMatch.Groups[1].Value, out var id)) continue;

            // Extract name: 'SomeName' (single-quoted in scan output)
            var nameMatch = Regex.Match(line, @"'([^']+)'");
            string? name = nameMatch.Success ? nameMatch.Groups[1].Value : null;

            // Extract AutomationId: [SomeId] (bracketed in scan output)
            var aidMatch = Regex.Match(line, @"\[([^\]]+)\]");
            string? aid = aidMatch.Success ? aidMatch.Groups[1].Value : null;

            // Extract control type: common type keywords
            string? controlType = null;
            var typePatterns = new[] {
                "TextBox", "Edit", "CheckBox", "RadioButton", "Button", "ComboBox",
                "Slider", "TrackBar", "ProgressBar", "TabItem", "TabPage", "Tab",
                "Expander", "ToggleButton", "TreeView", "TreeItem",
                "ListView", "ListItem", "DataGrid", "DataItem",
                "Menu", "MenuItem", "RichTextBox", "RichEdit",
                "PasswordBox", "DatePicker", "DateTimePicker",
                "ScrollBar", "ListBox", "Spinner", "NumericUpDown",
                "GroupBox", "Group", "StatusBar", "ToolBar",
                "Hyperlink", "Link", "Image", "Calendar",
                "InkCanvas", "Thumb", "RepeatButton", "Separator"
            };
            foreach (var tp in typePatterns)
            {
                if (line.Contains(tp, StringComparison.OrdinalIgnoreCase))
                {
                    controlType = tp;
                    break;
                }
            }

            list.Add(new ScannedElement(id, line.Trim(), name, aid, controlType));
        }
        return list;
    }

    /// <summary>Find first element whose line contains the given text (case-insensitive).</summary>
    public static long? FindId(string scanData, string text)
    {
        foreach (var line in scanData.Split('\n'))
        {
            if (!line.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(line, @"ID:(\d+)");
            if (m.Success && long.TryParse(m.Groups[1].Value, out var id)) return id;
        }
        return null;
    }

    /// <summary>Find all elements matching a control type keyword.</summary>
    public static List<ScannedElement> FindByType(string scanData, string typeName)
    {
        return ParseAll(scanData)
            .Where(e => e.RawLine.Contains(typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
