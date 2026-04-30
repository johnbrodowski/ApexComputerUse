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
        // -- TextBox / PasswordBox -----------------------------------------

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE    = 0x0001;
        private const uint SWP_NOZORDER  = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static void BringContainerWindowToFront(AutomationElement el)
        {
            var cur = el;
            while (cur != null)
            {
                if (cur.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Window)
                {
                    var hwnd = cur.Properties.NativeWindowHandle.ValueOrDefault;
                    if (hwnd != IntPtr.Zero)
                    {
                        // Move off-screen windows into view so keyboard input reaches them.
                        var rect = cur.Properties.BoundingRectangle.ValueOrDefault;
                        if (rect.X < -100 || rect.Y < -100)
                        {
                            SetWindowPos(hwnd, IntPtr.Zero, 100, 100, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
                            Thread.Sleep(100);
                        }
                        SetForegroundWindow(hwnd);
                    }
                    return;
                }
                try { cur = cur.Parent; } catch { return; }
            }
        }

        /// <summary>
        /// Types text into an element, then sends any {KEY} tokens embedded in the string.
        /// Text is entered via clipboard paste; key tokens (e.g. {ENTER}, {TAB}) are pressed afterward.
        /// Use this for inputs like "search query{ENTER}" where text and keys are combined.
        /// </summary>
        public void EnterTextWithKeys(AutomationElement el, string input)
        {
            input = NormalizeBracketKeyTokens(input);
            if (!input.Contains('{'))
            {
                EnterText(el, input);
                return;
            }

            var plainText  = new System.Text.StringBuilder();
            var keyTokens  = new System.Text.StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                if (input[i] == '{')
                {
                    int end = input.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        string name = input.Substring(i + 1, end - i - 1);
                        if (ParseVirtualKey(name).HasValue)
                        {
                            keyTokens.Append('{').Append(name).Append('}');
                            i = end + 1;
                            continue;
                        }
                    }
                }
                plainText.Append(input[i]);
                i++;
            }

            if (plainText.Length > 0)
                EnterText(el, plainText.ToString());

            if (keyTokens.Length > 0)
            {
                BringContainerWindowToFront(el);
                el.Focus();
                Thread.Sleep(FocusDelayMs);
                SendBraceKeys(keyTokens.ToString());
            }
        }

        public void EnterText(AutomationElement el, string text)
        {
            // Prefer Value pattern (same as setvalue) - avoids keyboard/focus issues entirely.
            if (el.Patterns.Value.TryGetPattern(out var vp) && !vp.IsReadOnly.ValueOrDefault)
            {
                vp.SetValue(text);
                return;
            }

            // Keyboard.Type can leave Shift latched after shifted characters (e.g. &, $).
            // Clipboard paste avoids keyboard simulation entirely and handles all characters correctly.
            Exception? clipEx = null;
            var sta = new Thread(() =>
            {
                try { System.Windows.Forms.Clipboard.SetText(text, System.Windows.Forms.TextDataFormat.UnicodeText); }
                catch (Exception e) { clipEx = e; }
            });
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            sta.Join();
            if (clipEx != null)
            {
                // Fallback: original keyboard typing
                el.AsTextBox().Enter(text);
                return;
            }
            BringContainerWindowToFront(el);
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
            Thread.Sleep(50);
        }

        public void SelectAllText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        }

        public void CopyText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
        }

        public void CutText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_X);
        }

        public void PasteText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
        }

        public void UndoText(AutomationElement el)
        {
            el.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        }

        public void ClearText(AutomationElement el)
        {
            SelectAllText(el);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Keyboard.Release(VirtualKeyShort.DELETE);
        }

        public void InsertTextAtCaret(AutomationElement el, string text)
        {
            el.Focus();
            Keyboard.Type(text);
        }

        // -- Keyboard ------------------------------------------------------

        public void SendKey(VirtualKeyShort key) =>
            Keyboard.Press(key);

        public void SendKeys(string text) =>
            Keyboard.Type(text);

        public void SendShortcut(VirtualKeyShort modifier, VirtualKeyShort key) =>
            Keyboard.TypeSimultaneously(modifier, key);

        /// <summary>
        /// Focuses the element then sends keys with full notation support:
        ///   - {KEY} tokens: {CTRL}, {ALT}, {SHIFT}, {ENTER}, {TAB}, {DELETE}, {F5}, etc.
        ///   - Modifier combos: "Ctrl+A", "Alt+F4", "Shift+Tab"
        ///   - Single key name: "Enter", "Escape", "Tab", "F5"
        ///   - Literal text: anything else is typed character by character
        /// </summary>
        public void SendKeysEnhanced(AutomationElement el, string keys)
        {
            keys = NormalizeBracketKeyTokens(keys);

            el.Focus();
            Thread.Sleep(FocusDelayMs);

            // Handle "Modifier+{KEY}" mixed notation - convert to "{MODIFIER}{KEY}" for SendBraceKeys
            if (keys.Contains('+') && keys.Contains('{'))
            {
                int plusIdx  = keys.IndexOf('+');
                int braceIdx = keys.IndexOf('{');
                if (plusIdx < braceIdx)
                {
                    string modWord = keys[..plusIdx].Trim();
                    string rest    = keys[(plusIdx + 1)..].Trim();
                    var modVk = ParseVirtualKey(modWord);
                    if (modVk != null && rest.StartsWith('{'))
                    {
                        SendBraceKeys("{" + modWord.ToUpper() + "}" + rest);
                        return;
                    }
                }
            }

            if (keys.Contains('{'))
            {
                SendBraceKeys(keys);
                return;
            }

            if (keys.Contains('+'))
            {
                var parts    = keys.Split('+', 2);
                var modifier = ParseVirtualKey(parts[0].Trim());
                var key      = ParseVirtualKey(parts[1].Trim());
                if (modifier != null && key != null)
                {
                    Keyboard.TypeSimultaneously(modifier.Value, key.Value);
                    return;
                }
            }

            var vk = ParseVirtualKey(keys);
            if (vk != null)
            {
                Keyboard.Press(vk.Value);
                Keyboard.Release(vk.Value);
            }
            else
            {
                Keyboard.Type(keys);
            }
        }

        /// <summary>
        /// Normalizes bracket-style key tokens (for example [Enter]) into the
        /// brace style supported by SendBraceKeys ({Enter}).
        /// Only recognized key names are converted; unknown bracketed text is left unchanged.
        /// </summary>
        internal static string NormalizeBracketKeyTokens(string keys)
        {
            if (string.IsNullOrEmpty(keys) || !keys.Contains('['))
                return keys;

            var sb = new System.Text.StringBuilder(keys.Length);
            int i = 0;
            while (i < keys.Length)
            {
                if (keys[i] == '[')
                {
                    int end = keys.IndexOf(']', i + 1);
                    if (end > i + 1)
                    {
                        string token = keys[(i + 1)..end].Trim();
                        if (ParseVirtualKey(token).HasValue)
                        {
                            sb.Append('{').Append(token).Append('}');
                            i = end + 1;
                            continue;
                        }
                    }
                }

                sb.Append(keys[i]);
                i++;
            }

            return sb.ToString();
        }

        private static void SendBraceKeys(string keys)
        {
            int i = 0;
            VirtualKeyShort? heldModifier = null;
            while (i < keys.Length)
            {
                if (keys[i] == '{')
                {
                    int end = keys.IndexOf('}', i + 1);
                    if (end < 0) break;
                    string name = keys.Substring(i + 1, end - i - 1);
                    var vk = ParseVirtualKey(name);
                    if (vk.HasValue)
                    {
                        bool isMod = vk.Value is VirtualKeyShort.CONTROL or VirtualKeyShort.ALT or VirtualKeyShort.SHIFT;
                        if (isMod)
                            heldModifier = vk.Value;
                        else if (heldModifier.HasValue)
                        {
                            Keyboard.TypeSimultaneously(heldModifier.Value, vk.Value);
                            heldModifier = null;
                        }
                        else
                        {
                            Keyboard.Press(vk.Value);
                            Keyboard.Release(vk.Value);
                        }
                    }
                    i = end + 1;
                }
                else
                {
                    if (heldModifier.HasValue)
                    {
                        var charVk = ParseVirtualKey(keys[i].ToString());
                        if (charVk.HasValue)
                            Keyboard.TypeSimultaneously(heldModifier.Value, charVk.Value);
                        else
                            Keyboard.Type(keys[i].ToString());
                        heldModifier = null;
                    }
                    else
                        Keyboard.Type(keys[i].ToString());
                    i++;
                }
            }
        }

        private static VirtualKeyShort? ParseVirtualKey(string name) =>
            name.ToLowerInvariant() switch
            {
                "enter" or "return"   => VirtualKeyShort.RETURN,
                "tab"                 => VirtualKeyShort.TAB,
                "escape" or "esc"     => VirtualKeyShort.ESCAPE,
                "backspace" or "back" => VirtualKeyShort.BACK,
                "delete" or "del"     => VirtualKeyShort.DELETE,
                "space"               => VirtualKeyShort.SPACE,
                "up"                  => VirtualKeyShort.UP,
                "down"                => VirtualKeyShort.DOWN,
                "left"                => VirtualKeyShort.LEFT,
                "right"               => VirtualKeyShort.RIGHT,
                "home"                => VirtualKeyShort.HOME,
                "end"                 => VirtualKeyShort.END,
                "pageup"              => VirtualKeyShort.PRIOR,
                "pagedown"            => VirtualKeyShort.NEXT,
                "insert"              => VirtualKeyShort.INSERT,
                "ctrl" or "control"   => VirtualKeyShort.CONTROL,
                "alt"                 => VirtualKeyShort.ALT,
                "shift"               => VirtualKeyShort.SHIFT,
                "f1"  => VirtualKeyShort.F1,  "f2"  => VirtualKeyShort.F2,
                "f3"  => VirtualKeyShort.F3,  "f4"  => VirtualKeyShort.F4,
                "f5"  => VirtualKeyShort.F5,  "f6"  => VirtualKeyShort.F6,
                "f7"  => VirtualKeyShort.F7,  "f8"  => VirtualKeyShort.F8,
                "f9"  => VirtualKeyShort.F9,  "f10" => VirtualKeyShort.F10,
                "f11" => VirtualKeyShort.F11, "f12" => VirtualKeyShort.F12,
                _ => name.Length == 1 && char.IsLetterOrDigit(name[0])
                     ? (VirtualKeyShort?)((VirtualKeyShort)char.ToUpper(name[0]))
                     : null
            };

    }
}

