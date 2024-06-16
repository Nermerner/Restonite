using FrooxEngine;
using HarmonyLib;
using MonkeyLoader.Patching;
using MonkeyLoader.Resonite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Restonite
{
    [HarmonyPatchCategory(nameof(RestoniteMod))]
    [HarmonyPatch(typeof(DevCreateNewForm), nameof(DevCreateNewForm.AddAction), new Type[] { typeof(string), typeof(string), typeof(Action<Slot>) })]
    internal class RestoniteMod : ResoniteMonkey<RestoniteMod>
    {
        #region Public Fields

        public const string WIZARD_TITLE = "Statue System Wizard (Mod)";

        #endregion

        #region Public Methods

        public static void Prefix(string path, string name, ref CategoryNode<DevCreateNewForm.CategoryItem> ___root)
        {
            var count = ___root.GetSubcategory(path)._elements.Count(x => x.name == name);
            Logger.Info(() => $"Removing {count} duplicates for {path}/{name}");
            ___root.GetSubcategory(path)._elements.RemoveAll(x => x.name == name);
        }

        #endregion

        #region Protected Methods

        // The options for these should be provided by your game's game pack.
        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        protected override bool OnEngineReady()
        {
            Harmony.PatchAll();

            DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => StatueSystemWizard.GetOrCreateWizard(x,
                msg => Logger.Debug(() => msg),
                msg => Logger.Info(() => msg),
                msg => Logger.Warn(() => msg),
                msg => Logger.Error(() => msg)));

            return true;
        }

        protected override bool OnLoaded()
        {
            return true;
        }

        #endregion
    }
}
