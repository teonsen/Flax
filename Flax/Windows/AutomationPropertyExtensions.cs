using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flax.Windows
{
    public static class AutomationPropertyExtensions
    {
        public static string ToDisplayText<T>(this FlaUI.Core.IAutomationProperty<T> automationProperty)
        {
            T value;
            var success = automationProperty.TryGetValue(out value);
            //return success ? (value == null ? String.Empty : value.ToString()) : "Not Supported";
            return success ? (value == null ? String.Empty : value.ToString()) : "";
        }
    }
}
