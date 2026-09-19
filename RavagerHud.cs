using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using RoR2.UI;
using UnityEngine;

namespace RavagerNoSacrifice
{
    internal static class RavagerHud
    {
        private const string LookingGlassGuid = "droppod.lookingglass";
        private static Type controllerType;
        private static Type ringGaugeType;
        private static Type oldGaugeType;
        private static MethodInfo hudSetup;
        private static ManualLogSource logger;
        private static bool active;
        private static float nextCheck;

        internal static void Install(ManualLogSource log)
        {
            logger = log;
            active = Chainloader.PluginInfos.ContainsKey(LookingGlassGuid);
            if (!active)
                return;
            controllerType = AccessTools.TypeByName("RedGuyMod.Content.Components.RedGuyController");
            ringGaugeType = AccessTools.TypeByName("RedGuyMod.Content.Components.BloodGauge2");
            oldGaugeType = AccessTools.TypeByName("RedGuyMod.Content.Components.BloodGauge");
            hudSetup = AccessTools.Method(AccessTools.TypeByName("RedGuyMod.Content.Survivors.RedGuy"), "HUDSetup");
            if (controllerType == null || ringGaugeType == null || oldGaugeType == null || hudSetup == null)
            {
                active = false;
                logger.LogWarning("Ravager's Blood Well HUD types were not found.");
                return;
            }
            logger.LogInfo("LookingGlass Blood Well compatibility loaded.");
        }

        internal static void Uninstall()
        {
            active = false;
            logger = null;
        }

        internal static void Tick()
        {
            if (!active || Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 1f;
            foreach (var hud in HUD.readOnlyInstanceList)
                Repair(hud);
        }

        private static void Repair(HUD hud)
        {
            if (!hud || !hud.targetBodyObject || !hud.targetBodyObject.GetComponent(controllerType))
                return;
            var gauges = hud.GetComponentsInChildren(ringGaugeType, true);
            var oldGauges = hud.GetComponentsInChildren(oldGaugeType, true);
            if (gauges.Length == 0 && oldGauges.Length == 0)
            {
                try
                {
                    hudSetup.Invoke(null, new object[] { hud });
                    gauges = hud.GetComponentsInChildren(ringGaugeType, true);
                    oldGauges = hud.GetComponentsInChildren(oldGaugeType, true);
                }
                catch (TargetInvocationException exception)
                {
                    logger.LogDebug(exception.InnerException ?? exception);
                    return;
                }
            }
            foreach (Component gauge in gauges)
                BringForward(gauge.transform.parent ? gauge.transform.parent : gauge.transform, hud);
            foreach (Component gauge in oldGauges)
                BringForward(gauge.transform, hud);
        }

        private static void BringForward(Transform root, HUD hud)
        {
            if (!root)
                return;
            root.gameObject.SetActive(true);
            if (!root.parent || !root.parent.gameObject.activeInHierarchy)
            {
                root.SetParent(hud.transform, false);
                var rect = root as RectTransform;
                if (rect)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(65f, -75f);
                    rect.localScale = new Vector3(0.4f, 0.4f, 1f);
                }
            }
            root.SetAsLastSibling();
            var canvas = root.GetComponent<Canvas>();
            if (!canvas)
                canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }
    }
}
