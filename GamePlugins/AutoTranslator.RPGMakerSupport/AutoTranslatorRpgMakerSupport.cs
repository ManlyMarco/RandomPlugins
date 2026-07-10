using System.Threading;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using XUnity.AutoTranslator.Plugin.Core;

namespace AutoTranslator.RPGMakerSupport
{
    [BepInPlugin(GUID, GUID, Version)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", "5.6")]
    public class AutoTranslatorRpgMakerSupport : BaseUnityPlugin
    {
        private static ITranslator _translator;
        public const string GUID = "AutoTranslator.RPGMakerSupport";
        public const string Version = Constants.Version;

        private void Start()
        {
            _translator = XUnity.AutoTranslator.Plugin.Core.AutoTranslator.Default;
            Harmony.CreateAndPatchAll(typeof(AutoTranslatorRpgMakerSupport));
        }

        //RPGMaker.Codebase.Runtime.Common.WindowBase.Init() : void @06000714
        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(RPGMaker.Codebase.Runtime.Common.WindowBase), nameof(RPGMaker.Codebase.Runtime.Common.WindowBase.Init))]
        //private static void WindowBaseInitPrefix(RPGMaker.Codebase.Runtime.Common.WindowBase __instance)
        //{
        //    if (!__instance.gameObject.name.Contains("XUAIGNORETREE"))
        //        __instance.gameObject.name += " XUAIGNORETREE";
        //}

        //RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter.Processer
        //public void InitControl(GameObject parent, string messageText, string fontName, int fontSize, Color fontColor, GameObject goldWindow, bool isAllSkip)
        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter.Processer), nameof(RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter.Processer.InitControl))]
        //private static void InitControlPrefix(RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter.Processer __instance, GameObject parent, ref string messageText, string fontName, int fontSize, Color fontColor, GameObject goldWindow, bool isAllSkip)

        private static bool _isLooping = false;

        //public void InitControl(GameObject parent, string messageText, string fontName, int fontSize, Color fontColor, GameObject goldWindow, bool isAllSkip)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter), nameof(RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter.InitControl))]
        private static bool InitControlPrefix(RPGMaker.Codebase.Runtime.Common.ControlCharacter.ControlCharacter __instance, GameObject parent, ref string messageText, string fontName, int fontSize, Color fontColor, GameObject goldWindow, bool isAllSkip)
        {
            if (_translator.TryTranslate(messageText, out var translatedText))
            {
                messageText = translatedText;
                return true;
            }
            else
            {
                //if (_isLooping)
                //{
                //    _isLooping = false;
                //    return true;
                //}
                //TranslationResult result2 = null;
                //_isLooping = true;
                //var txt = messageText;
                _translator.TranslateAsync(messageText, result =>
                {
                    // BUG Doing anything to the message after the fact softlocks the game, can't find a way to update the text after
                    //__instance.InitControl(parent, txt, fontName, fontSize, fontColor, goldWindow, isAllSkip);
                    //_isLooping = false;
                });

                //return false;
                return true;
            }
        }
    }
}
