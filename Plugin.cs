using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using EntityStates;
using HarmonyLib;
using LocalTweaks;
using RiskOfOptions;
using RoR2;
using UnityEngine;

namespace RavagerNoSacrifice
{
    [BepInPlugin(Guid, Name, "1.3.0")]
    [BepInDependency("com.rob.Ravager")]
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInDependency("droppod.lookingglass", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.youssef.RavagerNoSacrifice";
        public const string Name = "Ravager No Sacrifice";
        private static ConfigEntry<int> healthCost;
        private static ConfigEntry<bool> quickNullifyCooldown;
        private static PropertyInfo fixedAge;
        private static PropertyInfo cooldownOverride;
        private readonly Harmony harmony = new Harmony(Guid);

        private void Awake()
        {
            healthCost = TweakSettings.Slider(Config, "Twisted Mutation", "Health cost percent", 0, 0, 10,
                "Maximum percent of full combined health spent on the charged aerial blink. Partial charge still costs proportionally less. Applies on the next blink.", Guid, Name);
            quickNullifyCooldown = TweakSettings.Checkbox(Config, "Nullify", "Short-release cooldown", true,
                "Releasing Nullify in under two seconds gives it a four-second base cooldown. Longer charges keep the original cooldown.", Guid, Name);
            ModSettingsManager.SetModDescription("Configure Twisted Mutation and Nullify, rebuild the Blood Well display, and add Ravager skill details to LookingGlass.", Guid, Name);
            var icon = SettingsIcon.Load("RavagerNoSacrifice.SettingsIcon.png");
            if (icon)
                ModSettingsManager.SetModIcon(icon, Guid, Name);
            try
            {
                fixedAge = AccessTools.Property(typeof(EntityState), "fixedAge");
                cooldownOverride = AccessTools.Property(typeof(GenericSkill), "cooldownOverride");
                if (fixedAge == null)
                    throw new MissingMemberException("EntityState.fixedAge was not found.");
                harmony.Patch(PatchTools.Method("RedGuyMod.SkillStates.Ravager.BlinkBig", "OnEnter"),
                    transpiler: new HarmonyMethod(typeof(Plugin), nameof(ChangeHealthCost)));
                harmony.Patch(PatchTools.Method("RedGuyMod.SkillStates.Ravager.ChargeBeam", "OnExit"),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(ApplyQuickNullifyCooldown)));
                Logger.LogInfo("Ravager skill tweaks loaded.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchSelf();
                Logger.LogError($"The Ravager tweak was not applied. The installed Ravager version may have changed.\n{exception}");
            }
            RavagerHud.Install(Logger);
            LookingGlassSupport.Install(Logger);
        }

        private void OnDestroy()
        {
            RavagerHud.Uninstall();
            harmony.UnpatchSelf();
        }
        private static float CostFraction() => healthCost.Value / 100f;

        private static IEnumerable<CodeInstruction> ChangeHealthCost(IEnumerable<CodeInstruction> instructions)
        {
            var code = PatchTools.ReplaceFloat(instructions, 0.1f,
                AccessTools.Method(typeof(Plugin), nameof(CostFraction))).ToList();
            var takeDamage = AccessTools.Method(typeof(HealthComponent), nameof(HealthComponent.TakeDamage),
                new[] { typeof(DamageInfo) });
            var matches = code.Where(x => x.Calls(takeDamage)).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException("Twisted Mutation's health-cost call no longer matches.");
            matches[0].opcode = OpCodes.Call;
            matches[0].operand = AccessTools.Method(typeof(Plugin), nameof(ApplyHealthCost));
            return code;
        }

        private static void ApplyHealthCost(HealthComponent healthComponent, DamageInfo damageInfo)
        {
            if (healthCost.Value > 0 && damageInfo.damage > 0f)
                healthComponent.TakeDamage(damageInfo);
        }

        private static void ApplyQuickNullifyCooldown(EntityState __instance)
        {
            if (!quickNullifyCooldown.Value || (float)fixedAge.GetValue(__instance) >= 2f || !__instance.outer)
                return;
            var locator = __instance.outer.GetComponent<SkillLocator>();
            var skill = locator ? locator.utility : null;
            if (!skill || skill.activationState.stateType != __instance.GetType())
                return;
            float normalCooldown = skill.CalculateFinalRechargeInterval();
            float quickCooldown = normalCooldown * 4f / skill.baseRechargeInterval;
            if (cooldownOverride != null)
            {
                float previousOverride = (float)cooldownOverride.GetValue(skill);
                cooldownOverride.SetValue(skill, 4f);
                quickCooldown = skill.CalculateFinalRechargeInterval();
                cooldownOverride.SetValue(skill, previousOverride);
            }
            skill.rechargeStopwatch = Mathf.Max(skill.rechargeStopwatch, normalCooldown - quickCooldown);
        }
    }
}
