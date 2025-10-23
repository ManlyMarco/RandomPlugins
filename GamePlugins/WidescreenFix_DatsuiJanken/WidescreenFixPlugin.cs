using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WidescreenFix
{
    [BepInPlugin(GUID, DisplayName, Version)]
    public class WidescreenFixPlugin : BaseUnityPlugin
    {
        public const string GUID = "ManlyMarco.WidescreenFix_DatsuiJanken";
        public const string DisplayName = Constants.Name;
        public const string Version = Constants.Version;

        internal static new ManualLogSource Logger;

        private static List<Vector2Int> _customResolutions;
        private static Harmony _hi;

        private void Awake()
        {
            Logger = base.Logger;

            _hi = Harmony.CreateAndPatchAll(typeof(WidescreenFixPlugin), nameof(WidescreenFixPlugin));
        }

#if DEBUG
        private void OnDestroy()
        {
            _hi?.UnpatchSelf();
        }
#endif

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(InitialSetting), nameof(InitialSetting.Start))]
        private static IEnumerable<CodeInstruction> StartPatch(IEnumerable<CodeInstruction> instructions)
        {
            // Run after config is loaded but before anything else in Start
            return new CodeMatcher(instructions)
                   .MatchForward(false, new CodeMatch(null, AccessTools.Method(typeof(InitialFile), "Loading")))
                   .ThrowIfInvalid("Loading not found")
                   .Advance(1)
                   .Insert(new CodeInstruction(OpCodes.Ldarg_0),
                           new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WidescreenFixPlugin), nameof(WidescreenFixPlugin.AddCustomResolutions))))
                   .Instructions();
        }

        private static void AddCustomResolutions(InitialSetting __instance)
        {
            try
            {
                // Fix wide resolutions breaking UI scaling
                var scaler = __instance.GetComponent<CanvasScaler>();
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                // Add custom resolutions to the dropdown
                _customResolutions = Screen.resolutions.Where(x => x.width > 1920).Select(x => new Vector2Int(x.width, x.height)).Distinct().OrderBy(x => x.x).ThenBy(x => x.y).ToList();
                var newResolutionNames = _customResolutions.Select(customResolution => $"{customResolution.x}x{customResolution.y}").ToList();
                Logger.LogInfo("Adding support for resolutions: " + string.Join(", ", newResolutionNames));
                __instance.resolutionDropdown.AddOptions(newResolutionNames);

                // Need to set the resolution dropdown value now, before the window toggle is set, or custom resolutions will be reset to default by the window toggle's event handler
                __instance.CheckResolution(__instance.iniF.resolutionWidth, __instance.iniF.resolutionHeight);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                _hi.UnpatchSelf();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InitialSetting), nameof(InitialSetting.CheckResolution))]
        [HarmonyWrapSafe]
        private static bool CheckResolutionOverride(InitialSetting __instance, int w, int h)
        {
            // Override resolution check if custom resolution is used otherwise it'll be overwritten
            if (w > 1920)
            {
                var customRes = _customResolutions.FindIndex(x => x.x == w && x.y == h);
                if (customRes >= 0)
                {
                    __instance.resolutionDropdown.value = customRes + 9;
                    Screen.SetResolution(w, h, !__instance.iniF.windowed);
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InitialSetting), nameof(InitialSetting.ChangeResolution))]
        [HarmonyWrapSafe]
        private static void ChangeResolutionPrefix(InitialSetting __instance)
        {
            var val = __instance.resolutionDropdown.value;
            // Original code doesn't do anything above 8 so it can be allowed to run afterwards
            if (val >= 9)
            {
                var item = _customResolutions[val - 9];
                var w = item.x;
                var h = item.y;

                Screen.SetResolution(w, h, !__instance.iniF.windowed);
                __instance.iniF.resolutionWidth = w;
                __instance.iniF.resolutionHeight = h;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CharaEdit), nameof(CharaEdit.Start))]
        private static bool CharaEditInitDelay(CharaEdit __instance)
        {
            // Need to delay the set up because canvas layout happens after this point and control
            // positions change resulting in color picker being offset when mouse dragging
            IEnumerator DelayedInit()
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                __instance.SetUp();
            }

            __instance.StartCoroutine(DelayedInit());
            return false;
        }
    }
}
