using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Restonite
{
    public class RestoniteMod : ResoniteMod
    {
        #region Public Fields

        public override string Name => "Restonite";
        public override string Author => "Nermerner, Uruloke";

        public static Version AssemblyVersion => typeof(RestoniteMod).Assembly.GetName().Version;
        public override string Version => $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}";

        public override string Link => "https://github.com/Nermerner/Restonite";

        public const string WIZARD_TITLE = "Statue System Wizard (Mod)";
        private const string _harmonyId = "uruloke.Restonite";

        #endregion

        #region Protected Methods

        // This is the method that should be used to unload your mod
        // This means removing patches, clearing memory that may be in use etc.
        static void BeforeHotReload()
        {
            // Unpatch Harmony
            Harmony harmony = new Harmony(_harmonyId);
            harmony.UnpatchAll(_harmonyId);

#if DEBUG
            ResoniteHotReloadLib.HotReloader.RemoveMenuOption("Editor", WIZARD_TITLE);
#endif
        }

        // This is called in the newly loaded assembly
        // Load your mod here like you normally would in OnEngineInit
        static void OnHotReload(ResoniteMod modInstance)
        {
            Setup();
        }

        private static void Setup()
        {
            var harmony = new Harmony(_harmonyId);
            harmony.PatchAll();

            DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => StatueSystemWizard.GetOrCreateWizard(x,
                msg => Debug(() => msg),
                msg => Msg(() => msg),
                msg => Warn(() => msg),
                msg => Error(() => msg)));
        }

        public override void OnEngineInit()
        {
#if DEBUG
            ResoniteHotReloadLib.HotReloader.RegisterForHotReload(this);
#endif

            Setup();
        }

#endregion
    }
}
