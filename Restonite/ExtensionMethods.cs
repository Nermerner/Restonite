using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Restonite
{
    internal static class ExtensionMethods
    {
        public static void Setup(this EnumMemberEditor editor, IField target)
        {
            UIBuilder ui = new UIBuilder(editor.Slot);
            RadiantUI_Constants.SetupEditorStyle(ui);
            editor.Setup(target, null, ui);
        }

        public static string ToUixLineEndings(this string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br>");
        }

        public static string ToNormalLineEndings(this string text)
        {
            return text.Replace("<br>", "\r\n");
        }
    }
}
