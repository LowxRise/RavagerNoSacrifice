using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LocalTweaks;

namespace RavagerNoSacrifice
{
    [BepInPlugin(Guid, Name, "1.0.0")]
    [BepInDependency("com.rob.Ravager")]
    [BepInDependency("com.rune580.riskofoptions")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.youssef.RavagerNoSacrifice";
        public const string Name = "Ravager No Sacrifice";
        private static ConfigEntry<int> healthCost;
        private readonly Harmony harmony = new Harmony(Guid);

        private void Awake()
        {
            healthCost = TweakSettings.Slider(Config, "Twisted Mutation", "Health cost percent", 0, 0, 10,
                "Maximum percent of full combined health spent on the charged aerial blink. Partial charge still costs proportionally less. Applies on the next blink.", Guid, Name);
            try
            {
                harmony.Patch(PatchTools.Method("RedGuyMod.SkillStates.Ravager.BlinkBig", "OnEnter"),
                    transpiler: new HarmonyMethod(typeof(Plugin), nameof(ChangeHealthCost)));
                Logger.LogInfo("Twisted Mutation health-cost tweak loaded.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchSelf();
                Logger.LogError($"The Ravager tweak was not applied. The installed Ravager version may have changed.\n{exception}");
            }
        }

        private void OnDestroy() => harmony.UnpatchSelf();
        private static float CostFraction() => healthCost.Value / 100f;

        private static IEnumerable<CodeInstruction> ChangeHealthCost(IEnumerable<CodeInstruction> instructions) =>
            PatchTools.ReplaceFloat(instructions, 0.1f, AccessTools.Method(typeof(Plugin), nameof(CostFraction)));
    }
}
