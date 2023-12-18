using BepInEx;
using Dissonance;
using HarmonyLib;
using GameNetcodeStuff;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace NameplateTweaks;

[BepInPlugin(modGUID, modName, modVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string modGUID = "taffyko.NameplateTweaks";
    public const string modName = "NameplateTweaks";
    public const string modVersion = "1.0.3";

    public static ConfigEntry<bool> ConfigEnableSpeakingIndicator;
    public static ConfigEntry<bool> ConfigVariableSpeakingIndicatorOpacity;
    public static ConfigEntry<bool> ConfigSpeakingIndicatorAlwaysVisible;

    public static ConfigEntry<float> ConfigNameplateScale;
    public static ConfigEntry<float> ConfigNameplateVisibilityDistance;
    public static ConfigEntry<bool> ConfigNameplateScaleWithDistance;

    public static ManualLogSource log;
    private readonly Harmony harmony = new Harmony(modGUID);

    public static Vector3? OriginalNameplateScale = new Vector3(-0.0025f, 0.0025f, 0.0025f);

    private void Awake()
    {
        log = BepInEx.Logging.Logger.CreateLogSource(modName);
        log.LogInfo($"Loading {modGUID}");
        ConfigEnableSpeakingIndicator = Config.Bind("Speaking Indicator", "EnableSpeakingIndicator", true, "Enable a voice-activity speaking indicator above player nameplates");
        ConfigVariableSpeakingIndicatorOpacity = Config.Bind("Speaking Indicator", "VariableSpeakingIndicatorOpacity", true, "Speaking indicator opacity changes depending on volume");
        ConfigSpeakingIndicatorAlwaysVisible = Config.Bind("Speaking Indicator", "SpeakingIndicatorAlwaysVisible", true, "Display speaking indicators even when the nameplate is hidden");

        ConfigNameplateScale = Config.Bind("Nameplate", "NameplateScale", 1.5f, "Nameplate size multiplier (1.0 is the vanilla size)");
        ConfigNameplateVisibilityDistance = Config.Bind("Nameplate", "NameplateVisibilityDistance", 20.0f, "Distance from the camera within which nameplates are visible (0.0 reverts to vanilla behavior). The length of the ship is ~20 units, for reference");
        ConfigNameplateScaleWithDistance = Config.Bind("Nameplate", "NameplateScaleWithDistance", false, "Scale nameplates so that they retain their apparent size as they get further away");

        // Plugin startup logic
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
    private void OnDestroy()
    {
        #if DEBUG
        log.LogInfo($"Unloading {modGUID}");
        harmony.UnpatchSelf();
        foreach (var o in FindObjectsOfType<SpeakingIndicator>()) {
            Destroy(o.gameObject);
        }
        foreach (var o in FindObjectsOfType<PlayerControllerB>()) {
            o.usernameBillboard.localScale = OriginalNameplateScale.Value;
        }
        #endif
    }
}

public class SpeakingIndicator : MonoBehaviour {
    public PlayerControllerB player;
    public Canvas canvas;
    public GameObject canvasItem;
    public CanvasGroup canvasItemAlpha;
    public static Dictionary<PlayerControllerB, SpeakingIndicator> speakingIndicators = new Dictionary<PlayerControllerB, SpeakingIndicator>();

    private static Texture2D speakingIconTexture = null;
    public static Texture2D GetSpeakingIconTexture() {
        if (speakingIconTexture != null) {
            return speakingIconTexture;
        }
        var pttIcon = GameObject.Find("PTTIcon");
        return pttIcon.GetComponent<Image>().sprite.texture;
    }

    // Creates a SpeakingIndicator object if it doesn't already exist for a given player
    public static SpeakingIndicator GetSpeakingIndicator(PlayerControllerB player) {
        speakingIndicators.TryGetValue(player, out var speakingIndicator);
        if (speakingIndicator == null) {
            var speakingIndicatorObject = new GameObject("SpeakingIndicator");
            speakingIndicatorObject.SetActive(false);
            speakingIndicator = speakingIndicatorObject.AddComponent<SpeakingIndicator>();
            speakingIndicator.player = player;
            speakingIndicator.transform.SetParent(player.transform, false);
            speakingIndicators.Add(player, speakingIndicator);
            speakingIndicatorObject.SetActive(true);
        }
        return speakingIndicator;
    }

    public void Awake() {
        // Create canvas item for the speaking indicator sprite, parent it to the player nameplate
        canvasItem = new GameObject("SpeakingIndicatorCanvasItem");
        canvasItem.transform.SetParent(player.usernameBillboard, false);
        canvasItem.AddComponent<CanvasRenderer>();
        canvasItemAlpha = canvasItem.AddComponent<CanvasGroup>();
        canvasItemAlpha.alpha = 0f;
        canvasItem.transform.localPosition = new Vector3(0f, 60f, 0f);

        var image = canvasItem.AddComponent<Image>();
        image.sprite = Sprite.Create(GetSpeakingIconTexture(), new Rect(0f,0f,260f,280f), new Vector2(130f,140f), 100.0f);
    }

    private float lexp(float a, float b, float t) {
        return Mathf.Lerp(a, b, Mathf.Exp(-t));
    }

    public void Update() {
        VoicePlayerState playerVoice = player.voicePlayerState;
        if (playerVoice != null) {
            // Display speaking indicator based on voice amplitude
            float detectedAmplitude = Mathf.Clamp(playerVoice.Amplitude * 35f, 0.0f, 1f);
            if (Plugin.ConfigVariableSpeakingIndicatorOpacity.Value) {
                // With variable opacity
                if (detectedAmplitude > 0.01f) {
                    canvasItemAlpha.alpha = lexp(canvasItemAlpha.alpha, detectedAmplitude, Time.deltaTime*100f);
                } else {
                    canvasItemAlpha.alpha = lexp(canvasItemAlpha.alpha, detectedAmplitude, Time.deltaTime*50f);
                }
            } else {
                // Without variable opacity
                if (detectedAmplitude > 0.05f) {
                    canvasItemAlpha.alpha = 1f;
                } else if (detectedAmplitude < 0.01f) {
                    canvasItemAlpha.alpha = 0f;
                }
            }
        }

        if (!Plugin.ConfigSpeakingIndicatorAlwaysVisible.Value) {
            canvasItemAlpha.alpha = Math.Min(canvasItemAlpha.alpha, player.usernameAlpha.alpha);
        }

        if (player.IsOwner) {
            // Hide own speaking indicator
            canvasItemAlpha.alpha = 0f;
        }

        if (!Plugin.ConfigEnableSpeakingIndicator.Value) {
            Destroy(gameObject);
        }
    }

    public void OnDestroy() {
        speakingIndicators.Remove(player);
        Destroy(canvasItem);
    }
}

[HarmonyPatch]
public class Patches {
    [HarmonyPatch(typeof(HUDManager), "UpdateSpectateBoxSpeakerIcons")]
    [HarmonyPostfix]
    public static void UpdateSpectateBoxSpeakerIcons(HUDManager __instance, ref Dictionary<Animator, PlayerControllerB> ___spectatingPlayerBoxes) {
        // Only show spectator speaking icon when push-to-talk button is pressed
        foreach (var (anim, player) in ___spectatingPlayerBoxes) {
            if (player.IsOwner) {
                if (IngamePlayerSettings.Instance?.settings?.pushToTalk ?? false) {
                    var pttPressed = IngamePlayerSettings.Instance.playerInput?.actions?.FindAction("VoiceButton", false)?.IsPressed() ?? false;
                    anim.SetBool("speaking", pttPressed);
                }
                break;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    [HarmonyPostfix]
    public static void PlayerUpdate(PlayerControllerB __instance) {
        __instance.usernameBillboardText.text = __instance.playerUsername;

        // Create speaking indicators if they don't already exist
        if (Plugin.ConfigEnableSpeakingIndicator.Value) {
            SpeakingIndicator.GetSpeakingIndicator(__instance);
        }
        
        // Make billboard follow player's head
        __instance.usernameBillboard.position = new Vector3(
            __instance.playerGlobalHead.position.x,
            __instance.playerGlobalHead.position.y + 0.55f,
            __instance.playerGlobalHead.position.z
        );

        if (__instance.IsOwner) {
            // Hide own nameplate
            __instance.usernameAlpha.alpha = 0f;
        } else {
            float distance = Vector3.Distance(
                __instance.gameplayCamera.transform.position,
                __instance.usernameBillboard.position
            );

            if (distance < Plugin.ConfigNameplateVisibilityDistance.Value) {
                __instance.usernameAlpha.alpha = 1f;
                __instance.usernameBillboardText.enabled = true;
                __instance.usernameCanvas.gameObject.SetActive(value: true);
            }

            // Nameplate scale
            var distanceFactor = 1.0f;
            if (Plugin.ConfigNameplateScaleWithDistance.Value) {
                distanceFactor = 1.0f + Math.Max(0.0f, (distance - 4f) * 0.11f);
            }
            __instance.usernameBillboard.localScale = Plugin.OriginalNameplateScale.Value * Plugin.ConfigNameplateScale.Value * distanceFactor;
        }
    }
}