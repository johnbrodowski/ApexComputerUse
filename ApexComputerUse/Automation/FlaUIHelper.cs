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
    /// <summary>
    /// Wraps FlaUI interactions for every common WPF/WinForms control type and pattern.
    /// All pattern access uses TryGetPattern to avoid exceptions on unsupported elements.
    /// </summary>
    public partial class ApexHelper : IDisposable
    {
        private readonly UIA3Automation _automation = new();

        private const int FocusDelayMs    = 50;
        private const int DragStepDelayMs = 100;


        public void Dispose() => _automation.Dispose();
    }
}
