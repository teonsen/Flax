using System;
using System.Diagnostics;
using System.Windows.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

namespace Flax.Windows
{
    public class HoverMode
    {
        private readonly AutomationBase _automation;
        private readonly DispatcherTimer _dispatcherTimer;
        private AutomationElement _currentHoveredElement;

        //public event Action<AutomationElement> ElementHovered;
        public event Action<UIElement> ElementSelected;

        public HoverMode()
        {
            //_automation = automation;
            _automation = new FlaUI.UIA3.UIA3Automation();
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += DispatcherTimerTick;
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
        }

        public void Start()
        {
            _currentHoveredElement = null;
            _dispatcherTimer.Start();
        }

        public void Stop()
        {
            _currentHoveredElement = null;
            _dispatcherTimer.Stop();
        }

        private void DispatcherTimerTick(object sender, EventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                var screenPos = Mouse.Position;
                var hoveredElement = _automation.FromPoint(screenPos);
                // Skip items in the current process
                // Like Inspect itself or the overlay window
                if (hoveredElement.Properties.ProcessId == Process.GetCurrentProcess().Id)
                {
                    return;
                }
                if (!Equals(_currentHoveredElement, hoveredElement))
                {
                    _currentHoveredElement = hoveredElement;
                    //ElementHovered?.Invoke(hoveredElement);
                }
                else
                {
                    HighlightElement(hoveredElement);
                }

                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
                {
                    ElementSelected?.Invoke(new UIElement(hoveredElement));
                }
            }
        }

        private void HighlightElement(AutomationElement automationElement)
        {
            Task.Run(() => automationElement.DrawHighlight(false, System.Drawing.Color.Red, TimeSpan.FromSeconds(1)));
        }

    }
}
