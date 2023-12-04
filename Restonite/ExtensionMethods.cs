using FrooxEngine;
using FrooxEngine.UIX;

namespace Restonite
{
    internal static class ExtensionMethods
    {
        #region Public Methods

        public static void Setup(this EnumMemberEditor editor, IField target)
        {
            UIBuilder ui = new UIBuilder(editor.Slot);
            RadiantUI_Constants.SetupEditorStyle(ui);
            editor.Setup(target, null, ui);
        }

        public static string ToLongString(this Component component)
        {
            if (component == null)
                return "null";
            else
                return $"{component.GetType().Name}/{component.ReferenceID} on {component.Slot.Name}";
        }

        public static string ToLongString(this IAssetProvider<Material> material)
        {
            if (material == null)
                return "null";
            else
                return $"{material.GetType().Name}/{material.ReferenceID} on {material.Slot.Name}";
        }

        public static string ToNormalLineEndings(this string text)
        {
            return text.Replace("<br>", "\r\n");
        }

        public static string ToShortString(this Component component)
        {
            if (component == null)
                return "null";
            else
                return $"{component.GetType().Name}/{component.ReferenceID}";
        }

        public static string ToShortString(this IAssetProvider<Material> material)
        {
            if (material == null)
                return "null";
            else
                return $"{material.GetType().Name}/{material.ReferenceID}";
        }

        public static string ToShortString(this Slot slot)
        {
            if (slot == null)
                return "null";
            else
                return $"{slot.Name}/{slot.ReferenceID}";
        }

        public static string ToUixLineEndings(this string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br>");
        }

        #endregion
    }
}
