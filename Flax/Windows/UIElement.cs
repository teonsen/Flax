using System;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Flax.Windows
{
    public class UIElement
    {
        private static FlaxWindow _fxW;
        private static Window _flaW;

        public UIElement(FlaxWindow fxW, Window flaW)
        {
            _fxW = fxW;
            _flaW = flaW;
        }

        public bool Click(string id, Enum findBy)
        {
            var e = GetTheElement(id, findBy);
            if (e != null)
            {
                e.Click();
                return true;
            }
            return false;
        }

        private AutomationElement GetTheElement(string id, Enum findBy)
        {
            if (_fxW.IsMinimized)
            {
                _fxW.Restore();
                _fxW.SetFlaUIWindow();
            }
            _fxW.Activate();
            AutomationElement ae = null;
            switch (findBy)
            {
                case FindBy.AutomationID:
                    ae = Retry.WhileNull(() => _flaW.FindFirstDescendant(cf => cf.ByAutomationId(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.ClassName:
                    ae = Retry.WhileNull(() => _flaW.FindFirstDescendant(cf => cf.ByClassName(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.HelpText:
                    ae = Retry.WhileNull(() => _flaW.FindFirstDescendant(cf => cf.ByHelpText(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.Name:
                    ae = Retry.WhileNull(() => _flaW.FindFirstDescendant(cf => cf.ByName(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.Text:
                    ae = Retry.WhileNull(() => _flaW.FindFirstDescendant(cf => cf.ByText(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.Value:
                    ae = Retry.WhileNull(() => _flaW.FindFirstDescendant(cf => cf.ByValue(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
            }
            return ae;
        }

    }
}
