using System;
using System.Collections;
using BepInEx.Logging;
using HarmonyLib;

namespace RavagerNoSacrifice
{
    internal static class LookingGlassSupport
    {
        internal static void Install(ManualLogSource logger)
        {
            var type = AccessTools.TypeByName("LookingGlass.ItemStatsNameSpace.ProcCoefficientData");
            if (type == null)
                return;
            try
            {
                var skills = AccessTools.Field(type, "skills")?.GetValue(null) as IDictionary;
                var extras = AccessTools.Field(type, "skillsAdditional")?.GetValue(null) as IDictionary;
                if (skills == null || extras == null)
                    throw new MissingFieldException(type.FullName, "skills");

                Add(skills, "ROB_RAVAGER_BODY_PRIMARY_SLASH_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_PRIMARY_SLASHCOMBO_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_SECONDARY_SPINSLASH_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_UTILITY_HEAL_NAME", -1f);
                Add(skills, "ROB_RAVAGER_BODY_UTILITY_BEAM_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_SPECIAL_GRAB_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_SPECIAL_PUNCH_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_SPECIAL_THROW_NAME", 1f);
                Add(skills, "ROB_RAVAGER_BODY_SPECIAL_GRAB_SCEPTER_NAME", 1f);
                Add(extras, "ROB_RAVAGER_BODY_UTILITY_BEAM_NAME", "\nTicks: <style=cIsDamage>30 * Attack Speed</style> per second");
                logger.LogInfo("Added Ravager's skill proc coefficients to LookingGlass.");
            }
            catch (Exception exception)
            {
                logger.LogWarning($"LookingGlass skill details were not added.\n{exception}");
            }
        }

        private static void Add(IDictionary dictionary, string key, object value) => dictionary[key] = value;
    }
}
