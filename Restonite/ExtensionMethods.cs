using FrooxEngine;
using FrooxEngine.UIX;

namespace Restonite;

internal static class ExtensionMethods
{
    public static void Setup(this EnumMemberEditor editor, IField target)
    {
        var ui = new UIBuilder(editor.Slot);
        RadiantUI_Constants.SetupEditorStyle(ui);
        editor.Setup(target, null, ui);
    }

    public static string ToLongString(this Component? component)
    {
        if (component is null)
            return "null";
        else
            return $"{component.GetType().Name}/{component.ReferenceID} on {component.Slot.Name}";
    }

    public static string ToLongString(this IAssetProvider<Material>? material)
    {
        if (material is null)
            return "null";
        else
            return $"{material.GetType().Name}/{material.ReferenceID} on {material.Slot.Name}";
    }

    public static string ToNormalLineEndings(this string text)
    {
        return text.Replace("<br>", "\r\n");
    }

    public static string ToShortString(this Component? component)
    {
        if (component is null)
            return "null";
        else
            return $"{component.GetType().Name}/{component.ReferenceID}";
    }

    public static string ToShortString(this IAssetProvider<Material>? material)
    {
        if (material is null)
            return "null";
        else
            return $"{material.GetType().Name}/{material.ReferenceID}";
    }

    public static string ToShortString(this Slot? slot)
    {
        if (slot is null)
            return "null";
        else
            return $"{slot.Name}/{slot.ReferenceID}";
    }

    public static string ToUixLineEndings(this string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br>");
    }
}
