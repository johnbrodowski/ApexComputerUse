using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        private static CommandResponse CmdHelp() => Ok("Commands", """
            find     window=<title|id> [id=<automationId|mapId>] [name=<name>] [type=<ControlType>]
            exec     action=<action> [value=<input>]
            ocr      [value=x,y,w,h]
            capture  [action=screen|window|element|elements] [value=id1,id2,...]
            draw     value=<JSON DrawRequest>
              canvas      blank|white|black|screen|window|element|<base64-png>
              overlay     true = also show on screen as a transparent overlay
              overlay_ms  ms before overlay auto-closes (default 5000; 0 = Esc to dismiss)
              width    canvas width  (default 800, ignored when canvas is an image)
              height   canvas height (default 600, ignored when canvas is an image)
              shapes   array of shape objects:
                type         rect|ellipse|circle|line|arrow|text|polygon
                x,y          position (circle: centre)
                w,h          size (rect/ellipse)
                r            radius (circle)
                x2,y2        end point (line/arrow)
                points       [x1,y1,x2,y2,…] (polygon)
                color        name or #RRGGBB  (default "red")
                fill         true/false (default false)
                stroke_width pen width (default 2)
                opacity      0.0–1.0  (default 1)
                corner_radius rounded corners for rect (default 0)
                dashed       true/false dashed stroke (default false)
                text         label string (type=text)
                font_size    pixels (default 14)
                font_bold    true/false
                background   label background colour (type=text)
                align        left|center|right (type=text)
            ai       action=<sub> ...
              init     model=<path> proj=<path>
              status
              describe [prompt=<text>]
              file     value=<path> [prompt=<text>]
              ask      prompt=<text>
            status
            windows
            elements [type=<ControlType>]
            uimap
            help

            Actions (for exec):
              --- Click / Mouse ---
              click                    smart click (Invoke→Toggle→SelectionItem→mouse)
              mouse-click              force mouse left click
              right-click
              double-click
              middle-click
              click-at   value=x,y     click at offset from element top-left
              hover
              drag       value=x,y     drag element to screen coordinates
              highlight                draw orange highlight for 1 second

              --- Keyboard ---
              type / enter  value=<text>
              insert        value=<text>   insert at caret
              keys          value=<keys>   {CTRL}/{ALT}/{SHIFT}/{KEY}, Ctrl+A, Enter, Tab, ...
              selectall, copy, cut, paste, undo, clear

              --- Focus / State ---
              focus
              isenabled                returns true/false
              isvisible                returns true/false
              describe                 full element property dump
              patterns                 list supported UIA patterns
              bounds                   bounding rectangle

              --- Text / Value ---
              gettext                  smart: Text pattern→Value→Name
              getvalue                 smart: Value→Text→LegacyIAccessible→Name
              setvalue   value=<text>  smart: Value→RangeValue→keyboard
              clearvalue               set value to empty
              appendvalue value=<text> append to current value
              getselectedtext          selected text via Text pattern

              --- Range / Slider ---
              setrange  value=<num>    set RangeValue pattern value
              getrange                 get current range value
              rangeinfo                min/max/step/largechange

              --- Toggle ---
              toggle                   toggle current state
              toggle-on                set to On
              toggle-off               set to Off
              gettoggle                get current toggle state (On/Off/Indeterminate)

              --- ExpandCollapse ---
              expand
              collapse
              expandstate              get current expand/collapse state

              --- Selection (SelectionItem) ---
              select-item              select via SelectionItem pattern
              addselect                add to multi-selection
              removeselect             remove from selection
              isselected               check if selected
              getselection             get selected items from container

              --- ComboBox / ListBox ---
              select     value=<text>  select item by text (multi-strategy)
              select-index value=<n>   select by zero-based index
              getitems                 list all items
              getselecteditem          get currently selected item text

              --- Window ---
              minimize
              maximize
              restore
              windowstate              Normal / Maximized / Minimized

              --- Transform ---
              move       value=x,y     move element via Transform pattern
              resize     value=w,h     resize element via Transform pattern

              --- Scroll ---
              scroll-up   value=<n>    mouse wheel up (default 3)
              scroll-down value=<n>    mouse wheel down
              scroll-left value=<n>    mouse horizontal scroll left
              scroll-right value=<n>   mouse horizontal scroll right
              scrollinto               scroll element into view (ScrollItem pattern)
              scrollpercent value=h,v  scroll to h/v percent (0–100)
              getscrollinfo            scroll position and range

              --- Grid / Table ---
              griditem  value=row,col  get item description at grid row,col
              gridinfo                 row and column counts
              griditeminfo             row/col/span for a grid item

              --- Screenshot / OCR ---
              screenshot / capture

              --- Wait ---
              wait  value=<automationId>
            """);

    }
}
