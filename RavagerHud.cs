using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using RoR2.HudOverlay;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace RavagerNoSacrifice
{
    internal static class RavagerHud
    {
        private static readonly Harmony harmony = new Harmony(Plugin.Guid + ".hud");
        private static Type controllerType;
        private static FieldInfo meterField;
        private static FieldInfo drainingField;
        private static ManualLogSource logger;

        internal static void Install(ManualLogSource log)
        {
            logger = log;
            try
            {
                controllerType = AccessTools.TypeByName("RedGuyMod.Content.Components.RedGuyController");
                meterField = AccessTools.Field(controllerType, "meter");
                drainingField = AccessTools.Field(controllerType, "draining");
                var hudSetup = AccessTools.Method(AccessTools.TypeByName("RedGuyMod.Content.Survivors.RedGuy"), "HUDSetup");
                if (controllerType == null || meterField == null || drainingField == null || hudSetup == null)
                    throw new MissingMemberException("Ravager's Blood Well members were not found.");
                harmony.Patch(hudSetup, prefix: new HarmonyMethod(typeof(RavagerHud), nameof(SkipOriginalGauge)));
                CharacterBody.onBodyStartGlobal += AddGauge;
                logger.LogInfo("Centered Blood Well overlay loaded.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchSelf();
                logger.LogError($"The Blood Well overlay was not installed.\n{exception}");
            }
        }

        internal static void Uninstall()
        {
            CharacterBody.onBodyStartGlobal -= AddGauge;
            harmony.UnpatchSelf();
            logger = null;
        }

        private static bool SkipOriginalGauge() => false;

        private static void AddGauge(CharacterBody body)
        {
            var target = body ? body.GetComponent(controllerType) : null;
            if (target && !body.GetComponent<RavagerMeterController>())
                body.gameObject.AddComponent<RavagerMeterController>().Initialize(target, meterField, drainingField, logger);
        }
    }

    internal sealed class RavagerMeterController : MonoBehaviour
    {
        private static GameObject overlayPrefab;
        private static string overlayChild;
        private static readonly Color idleColor = new Color32(152, 12, 37, 255);
        private static readonly Color drainColor = new Color32(255, 0, 46, 255);
        private static readonly int meterParameter = Animator.StringToHash("corruption");
        private static readonly int drainParameter = Animator.StringToHash("isCorrupted");
        private readonly List<ImageFillController> fills = new List<ImageFillController>();
        private readonly List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();
        private readonly List<Image> images = new List<Image>();
        private readonly List<Animator> animators = new List<Animator>();
        private Component target;
        private FieldInfo meterField;
        private FieldInfo drainingField;
        private OverlayController overlay;
        private ManualLogSource logger;

        internal void Initialize(Component target, FieldInfo meter, FieldInfo draining, ManualLogSource log)
        {
            this.target = target;
            meterField = meter;
            drainingField = draining;
            logger = log;
        }

        private void Start()
        {
            try
            {
                LoadOverlay();
                if (!overlayPrefab)
                    throw new MissingMemberException("Void Fiend's meter overlay was not found.");
                overlay = HudOverlayManager.AddOverlay(gameObject, new OverlayCreationParams
                {
                    prefab = overlayPrefab,
                    childLocatorEntry = overlayChild
                });
                overlay.onInstanceAdded += InstanceAdded;
                overlay.onInstanceRemove += InstanceRemoved;
                foreach (var instance in overlay.instancesList)
                    InstanceAdded(overlay, instance);
            }
            catch (Exception exception)
            {
                logger?.LogError($"The Blood Well meter could not be created.\n{exception}");
                Destroy(this);
            }
        }

        private static void LoadOverlay()
        {
            if (overlayPrefab)
                return;
            var body = Addressables.LoadAssetAsync<GameObject>(
                "RoR2/DLC1/VoidSurvivor/VoidSurvivorBody.prefab").WaitForCompletion();
            var controller = body ? body.GetComponent<VoidSurvivorController>() : null;
            if (!controller)
                return;
            overlayPrefab = controller.overlayPrefab;
            overlayChild = controller.overlayChildLocatorEntry;
        }

        private void Update()
        {
            if (!target)
                return;
            float meter = Mathf.Clamp((float)meterField.GetValue(target), 0f, 100f);
            float fraction = meter / 100f;
            foreach (var fill in fills)
                if (fill) fill.SetTValue(fraction);
            foreach (var text in texts)
                if (text) text.SetText(Mathf.FloorToInt(meter).ToString());
            var color = (bool)drainingField.GetValue(target) ? drainColor : idleColor;
            foreach (var animator in animators)
            {
                if (!animator)
                    continue;
                animator.SetFloat(meterParameter, meter);
                animator.SetBool(drainParameter, color == drainColor);
            }
            foreach (var image in images)
            {
                if (!image || image.type != Image.Type.Filled)
                    continue;
                var current = image.color;
                image.color = new Color(color.r, color.g, color.b, current.a);
            }
        }

        private void InstanceAdded(OverlayController _, GameObject instance)
        {
            foreach (var fill in instance.GetComponentsInChildren<ImageFillController>(true))
                if (!fills.Contains(fill)) fills.Add(fill);
            foreach (var text in instance.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (!texts.Contains(text)) texts.Add(text);
            foreach (var image in instance.GetComponentsInChildren<Image>(true))
                if (!images.Contains(image)) images.Add(image);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                if (!animators.Contains(animator)) animators.Add(animator);
        }

        private void InstanceRemoved(OverlayController _, GameObject instance)
        {
            fills.RemoveAll(fill => !fill || fill.transform.IsChildOf(instance.transform));
            texts.RemoveAll(text => !text || text.transform.IsChildOf(instance.transform));
            images.RemoveAll(image => !image || image.transform.IsChildOf(instance.transform));
            animators.RemoveAll(animator => !animator || animator.transform.IsChildOf(instance.transform));
        }

        private void OnDestroy()
        {
            if (overlay == null)
                return;
            overlay.onInstanceAdded -= InstanceAdded;
            overlay.onInstanceRemove -= InstanceRemoved;
            HudOverlayManager.RemoveOverlay(overlay);
            overlay = null;
        }
    }
}
