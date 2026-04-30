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
        // -- Mouse ---------------------------------------------------------

        private static void MoveToCenterOf(AutomationElement el)
        {
            try
            {
                var b = el.BoundingRectangle;
                if (!b.IsEmpty)
                    Mouse.MoveTo(new System.Drawing.Point((int)(b.X + b.Width / 2), (int)(b.Y + b.Height / 2)));
            }
            catch { }
        }

        public string ScrollUp(AutomationElement el, int amount = 3, bool visual = false)
        {
            string name = el.Properties.Name.ValueOrDefault ?? "";
            if (!visual && el.Patterns.Scroll.TryGetPattern(out var p) && p.VerticallyScrollable.ValueOrDefault)
            {
                for (int i = 0; i < amount; i++) p.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallDecrement);
                return $"Scrolled up {amount} at '{name}' (pattern)";
            }
            MoveToCenterOf(el);
            Mouse.Scroll(amount);
            return $"Scrolled up {amount} at '{name}' (mouse)";
        }

        public string ScrollDown(AutomationElement el, int amount = 3, bool visual = false)
        {
            string name = el.Properties.Name.ValueOrDefault ?? "";
            if (!visual && el.Patterns.Scroll.TryGetPattern(out var p) && p.VerticallyScrollable.ValueOrDefault)
            {
                for (int i = 0; i < amount; i++) p.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement);
                return $"Scrolled down {amount} at '{name}' (pattern)";
            }
            MoveToCenterOf(el);
            Mouse.Scroll(-amount);
            return $"Scrolled down {amount} at '{name}' (mouse)";
        }

        public string HorizontalScroll(AutomationElement el, int amount, bool visual = false)
        {
            string name = el.Properties.Name.ValueOrDefault ?? "";
            bool left = amount < 0;
            if (!visual && el.Patterns.Scroll.TryGetPattern(out var p) && p.HorizontallyScrollable.ValueOrDefault)
            {
                var dir = left ? ScrollAmount.SmallDecrement : ScrollAmount.SmallIncrement;
                for (int i = 0; i < Math.Abs(amount); i++) p.Scroll(dir, ScrollAmount.NoAmount);
                return $"Scrolled {(left ? "left" : "right")} {Math.Abs(amount)} at '{name}' (pattern)";
            }
            MoveToCenterOf(el);
            Mouse.HorizontalScroll(amount);
            return $"Scrolled {(left ? "left" : "right")} {Math.Abs(amount)} at '{name}' (mouse)";
        }

        public void DragAndDrop(AutomationElement source, AutomationElement target)
        {
            var from = source.GetClickablePoint();
            var to   = target.GetClickablePoint();
            Mouse.Drag(from, to);
        }

        public void DragAndDropToPoint(AutomationElement source, int x, int y)
        {
            var from = source.GetClickablePoint();
            Mouse.Drag(from, new System.Drawing.Point(x, y));
        }

        // -- Scroll pattern ------------------------------------------------

        public void ScrollIntoView(AutomationElement el) =>
            el.Patterns.ScrollItem.Pattern.ScrollIntoView();

        public void ScrollElement(AutomationElement el,
            ScrollAmount vertical, ScrollAmount horizontal = ScrollAmount.NoAmount) =>
            el.Patterns.Scroll.Pattern.Scroll(horizontal, vertical);

        /// <summary>Scrolls an element to a horizontal and vertical percent (0-100).</summary>
        public void ScrollByPercent(AutomationElement el, double hPercent, double vPercent)
        {
            if (!el.Patterns.Scroll.TryGetPattern(out var p))
                throw new InvalidOperationException("Scroll pattern not supported");
            p.SetScrollPercent(hPercent, vPercent);
        }

        public string GetScrollInfo(AutomationElement el)
        {
            if (!el.Patterns.Scroll.TryGetPattern(out var p))
                return "Scroll pattern not supported";
            return $"HorizontalPercent={p.HorizontalScrollPercent.ValueOrDefault:G}  " +
                   $"VerticalPercent={p.VerticalScrollPercent.ValueOrDefault:G}  " +
                   $"HScrollable={p.HorizontallyScrollable.ValueOrDefault}  " +
                   $"VScrollable={p.VerticallyScrollable.ValueOrDefault}";
        }

        // -- Transform pattern ---------------------------------------------

        /// <summary>Moves an element using the Transform pattern.</summary>
        public void MoveElement(AutomationElement el, double x, double y)
        {
            if (!el.Patterns.Transform.TryGetPattern(out var p))
                throw new InvalidOperationException("Transform pattern not supported");
            if (!p.CanMove.ValueOrDefault) throw new InvalidOperationException("Element cannot be moved");
            p.Move(x, y);
        }

        /// <summary>Resizes an element using the Transform pattern.</summary>
        public void ResizeElement(AutomationElement el, double width, double height)
        {
            if (!el.Patterns.Transform.TryGetPattern(out var p))
                throw new InvalidOperationException("Transform pattern not supported");
            if (!p.CanResize.ValueOrDefault) throw new InvalidOperationException("Element cannot be resized");
            p.Resize(width, height);
        }

        // -- Highlight -----------------------------------------------------

        public void HighlightElement(AutomationElement el)
        {
            _ = Task.Run(() =>
            {
                try { el.DrawHighlight(false, System.Drawing.Color.Orange, TimeSpan.FromSeconds(1)); }
                catch { }
            });
        }

    }
}

