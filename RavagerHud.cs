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
        private const string OverlayName = "RavagerBloodWellOverlay";
        private static readonly Harmony harmony = new Harmony(Plugin.Guid + ".hud");
        private static Type controllerType;
        private static Type ringGaugeType;
        private static FieldInfo assetBundleField;
        private static FieldInfo targetHudField;
        private static FieldInfo targetField;
        private static FieldInfo fillBarField;
        private static Type imageType;
        private static ManualLogSource logger;
        private static bool active;
        private static bool creationErrorReported;
        private static float nextCheck;

        internal static void Install(ManualLogSource log)
        {
            logger = log;
            active = Chainloader.PluginInfos.ContainsKey(LookingGlassGuid);
            if (!active)
                return;

            controllerType = AccessTools.TypeByName("RedGuyMod.Content.Components.RedGuyController");
            ringGaugeType = AccessTools.TypeByName("RedGuyMod.Content.Components.BloodGauge2");
            var assetsType = AccessTools.TypeByName("RedGuyMod.Modules.Assets");
            var hudSetup = AccessTools.Method(AccessTools.TypeByName("RedGuyMod.Content.Survivors.RedGuy"), "HUDSetup");
            assetBundleField = AccessTools.Field(assetsType, "mainAssetBundle");
            targetHudField = AccessTools.Field(ringGaugeType, "targetHUD");
            targetField = AccessTools.Field(ringGaugeType, "target");
            fillBarField = AccessTools.Field(ringGaugeType, "fillBar");
            imageType = AccessTools.TypeByName("UnityEngine.UI.Image");
            if (controllerType == null || ringGaugeType == null || hudSetup == null ||
                assetBundleField == null || targetHudField == null || targetField == null ||
                fillBarField == null || imageType == null)
            {
                active = false;
                logger.LogWarning("Ravager's Blood Well HUD types were not found.");
                return;
            }

            harmony.Patch(hudSetup, prefix: new HarmonyMethod(typeof(RavagerHud), nameof(UseRavagerSetup)));
            logger.LogInfo("LookingGlass Blood Well replacement loaded.");
        }

        internal static void Uninstall()
        {
            harmony.UnpatchSelf();
            active = false;
            logger = null;
        }

        internal static void Tick()
        {
            if (!active || Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.25f;
            foreach (var hud in HUD.readOnlyInstanceList)
                Repair(hud);
        }

        private static bool UseRavagerSetup() => !active;

        private static void Repair(HUD hud)
        {
            if (!hud || !hud.targetBodyObject || !hud.targetBodyObject.GetComponent(controllerType))
                return;

            var gauges = hud.GetComponentsInChildren(ringGaugeType, true);
            if (gauges.Length == 0)
            {
                var gauge = CreateGauge(hud);
                if (!gauge)
                    return;
                gauges = new[] { gauge };
            }

            foreach (Component gauge in gauges)
            {
                var root = gauge.transform.parent ? gauge.transform.parent : gauge.transform;
                PlaceInOverlay(root, hud);
            }
        }

        private static Component CreateGauge(HUD hud)
        {
            try
            {
                var bundle = assetBundleField.GetValue(null) as AssetBundle;
                var prefab = bundle ? bundle.LoadAsset<GameObject>("ChargeRing") : null;
                if (!prefab)
                    throw new MissingMemberException("Ravager's ChargeRing asset was not found.");

                var root = UnityEngine.Object.Instantiate(prefab);
                root.name = "RavagerBloodWell";
                PlaceInOverlay(root.transform, hud);
                var fillObject = root.transform.GetChild(0).gameObject;
                var gauge = fillObject.AddComponent(ringGaugeType);
                targetHudField.SetValue(gauge, hud);
                targetField.SetValue(gauge, hud.targetBodyObject.GetComponent(controllerType));
                fillBarField.SetValue(gauge, fillObject.GetComponent(imageType));
                creationErrorReported = false;
                return gauge;
            }
            catch (Exception exception)
            {
                if (!creationErrorReported)
                {
                    creationErrorReported = true;
                    logger.LogError($"The Blood Well gauge could not be created.\n{exception}");
                }
                return null;
            }
        }

        private static void PlaceInOverlay(Transform root, HUD hud)
        {
            if (!root)
                return;

            var overlay = hud.transform.Find(OverlayName);
            if (!overlay)
            {
                var parent = hud.transform.Find("MainContainer/MainUIArea/CrosshairCanvas");
                if (!parent)
                    parent = hud.transform;
                var overlayObject = new GameObject(OverlayName, typeof(RectTransform), typeof(Canvas));
                overlayObject.layer = parent.gameObject.layer;
                overlay = overlayObject.transform;
                overlay.SetParent(parent, false);
                var overlayRect = overlay as RectTransform;
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                var canvas = overlayObject.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 5000;
            }

            if (root.parent != overlay)
                root.SetParent(overlay, false);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            var rect = root as RectTransform;
            if (!rect)
                return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(65f, -75f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = new Vector3(0.4f, 0.4f, 1f);
        }
    }
}
