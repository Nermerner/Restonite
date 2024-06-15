using System;
using FrooxEngine;
using MonkeyLoader.Patching;
using MonkeyLoader.Resonite;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Restonite
{
    [HarmonyPatchCategory(nameof(Restonite))]
    [HarmonyPatch(typeof(DevCreateNewForm), nameof(DevCreateNewForm.AddAction), new Type[] { typeof(string), typeof(string), typeof(Action<Slot>) })]
    internal class RestoniteMod : ResoniteMonkey<RestoniteMod>
    {
        public const string WIZARD_TITLE = "Statue System Wizard (Mod)";

        // The options for these should be provided by your game's game pack.
        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        protected override bool OnEngineReady()
        {
            base.OnEngineReady();

            DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => StatueSystemWizard.GetOrCreateWizard(x,
                msg => Logger.Debug(() => msg),
                msg => Logger.Info(() => msg),
                msg => Logger.Warn(() => msg),
                msg => Logger.Error(() => msg)));

            return true;
        }

        [HarmonyPrefix]
        public static void Prefix(string path, string name, ref CategoryNode<DevCreateNewForm.CategoryItem> ___root)
        {
            ___root.GetSubcategory(path)._elements.RemoveAll(x => x.name == name);
        }
    }
}
