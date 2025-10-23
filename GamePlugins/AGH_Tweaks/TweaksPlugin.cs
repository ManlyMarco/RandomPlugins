using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AGH_Tweaks
{
    [BepInPlugin(GUID, DisplayName, Version)]
    [BepInDependency(ConfigurationManager.ConfigurationManager.GUID, ConfigurationManager.ConfigurationManager.Version)]
    [BepInDependency("com.bepis.messagecenter", BepInDependency.DependencyFlags.SoftDependency)] // Make sure the warning message insinde Awake is displayed in UI if it happens
    public class TweaksPlugin : BaseUnityPlugin
    {
        public const string Version = Constants.Version;
        public const string GUID = "AGH_Tweaks";
        public const string DisplayName = Constants.Description;

        internal static new ManualLogSource Logger;

        private static ConfigEntry<bool> _disableSubtitles;
        private static ConfigEntry<bool> _disableEnglish;
        private static ConfigEntry<bool> _showDebugMode;

        private void Awake()
        {
            Logger = base.Logger;

            if (Type.GetType("ZZHelper, Assembly-CSharp", false) == null)
            {
                Logger.Log(LogLevel.Warning | LogLevel.Message, "This plugin requires the English translation + uncensor mod (the custom Assembly-CSharp.dll file) to be installed.");
                enabled = false;
                return;
            }

            _disableEnglish = Config.Bind("Translation", "Disable English translation", false, "Use original Japanese text while keeping other features.\nAlso disables subtitles since no Japanese text is available for them.\nA game restart is required for chamges to take full effect.");
            _disableSubtitles = Config.Bind("Translation", "Disable H scene subtitles", false, "Do not show subtitles inside H scenes.");
            _showDebugMode = Config.Bind("General", "Show debug mode button", false, "Show a 'Open debug mode' button on the title screen (in bottom left corner).\nThe debug screen allows you to skip to any part of the story and change any parameter of the current save file. It's essentially a developer-provided trainer.");

            Harmony.CreateAndPatchAll(typeof(Hooks));

            _configMan = (ConfigurationManager.ConfigurationManager)Chainloader.PluginInfos[ConfigurationManager.ConfigurationManager.GUID].Instance;
        }

        private static GameObject _currentDebugText;
        private static GameObject _currentMainText;
        private static GUIStyle _debugButtonStyle;
        private ConfigurationManager.ConfigurationManager _configMan;

        private void OnGUI()
        {
            // Only run in title screen
            // todo add some sort of fade in and wait for actual title screen to be visible?
            if (!_currentDebugText) return;

            GUI.depth = int.MaxValue;

            const int edgeOffset = 10;

            // The game moves UI elemets off screen instead of disabling them
            var debugVisible = _currentDebugText.transform.localPosition.x < 5000;
            // Don't obscure the debug screen
            if (debugVisible) return;

            if (_showDebugMode.Value)
            {
                if (_debugButtonStyle == null)
                {
                    _debugButtonStyle = new GUIStyle(GUI.skin.label);
                    _debugButtonStyle.fontSize = 17;
                    _debugButtonStyle.alignment = TextAnchor.LowerLeft;
                }

                const int width = 160;
                const int height = 30;
                if (IMGUIUtils.DrawButtonWithShadow(new Rect(edgeOffset, Screen.height - edgeOffset - height, width, height), new GUIContent("Open debug mode"), _debugButtonStyle, 0.8f, new Vector2(1, 1)))
                {
                    _currentDebugText.transform.localPosition = new Vector3(0f, 0f, 0f);
                    _currentMainText.transform.localPosition = new Vector3(5000f, 0f, 0f);
                }
            }

            if (!_configMan.DisplayingWindow)
            {
                if (IMGUIUtils.DrawButtonWithShadow(new Rect(edgeOffset, edgeOffset, width: 250, height: 24), new GUIContent("Press F1 to open plugin/mod settings"), GUI.skin.label, 0.8f, new Vector2(1, 1)))
                    _configMan.DisplayingWindow = true;
            }
        }

        private static class Hooks
        {
            #region Disabling English translation

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ZZHelper), "loadTexture")]
            private static bool LoadTextureOverride(string filePath)
            {
                if (_disableEnglish.Value)
                {
                    // Except the uncensor
                    if (!filePath.EndsWith("CH01_uterus.png", StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ZZHelper), nameof(ZZHelper.checkLabelText))]
            private static bool LabelTextOverride(string text, string source, ref string __result)
            {
                if (_disableEnglish.Value)
                {
                    __result = text;
                    return false;
                }

                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ZZHelper), nameof(ZZHelper.loadMasterScenario))]
            private static bool LoadMasterScenarioOverride()
            {
                return !_disableEnglish.Value;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ZZonScreen), nameof(ZZonScreen.OnGUI))]
            private static bool SubtitlesOverride()
            {
                return !_disableEnglish.Value && !_disableSubtitles.Value;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ZZHelper), nameof(ZZHelper.setWinTitleName))]
            private static bool SetWinTitleNameOverride()
            {
                return !_disableEnglish.Value;
            }

            #endregion

            #region Fix nullref spam

            [HarmonyFinalizer]
            [HarmonyPatch(typeof(MozaicSetUp), "Start")]
            private static Exception MozaNullrefFix1()
            {
                return null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MozaicSetUp), "Update")]
            private static bool MozaNullrefFix2(MozaicSetUp __instance)
            {
                return __instance.MozaObj != null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DanmenPixel), "Update")]
            private static bool MozaNullrefFix3(Renderer ___PC00_ute05_moza)
            {
                return ___PC00_ute05_moza != null;
            }

            #endregion

            #region Debug screen stuff

            [HarmonyPostfix]
            [HarmonyPatch(typeof(TitleFade), "Start")]
            private static void TitleFadeHook(GameObject ___DebugText, GameObject ___MainText)
            {
                _currentDebugText = ___DebugText;
                _currentMainText = ___MainText;
            }

            #endregion
        }
    }
}
