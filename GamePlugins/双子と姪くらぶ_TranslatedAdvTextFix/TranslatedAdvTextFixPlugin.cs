using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Utage;
using XUnity.AutoTranslator.Plugin.Core;

namespace TranslatedAdvTextFix
{
    [BepInPlugin(GUID, DisplayName, Version)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator")]
    public class TranslatedAdvTextFixPlugin : BaseUnityPlugin
    {
        public const string Version = Constants.Version;
        public const string GUID = "TranslatedAdvTextFix";
        public const string DisplayName = Constants.Name;

        private void Start()
        {
            Harmony.CreateAndPatchAll(typeof(TranslatedAdvTextFixPlugin));

        }

        private static HashSet<string> _seen = new HashSet<string>();

        //patch private static TextParserBase CreateTextParser(string text)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Utage.TextData), "CreateTextParser")]
        private static void CreateTextParser_Prefix(ref string text, TextParserBase __result, out bool __state)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                __state = true;
            }
            else if (AutoTranslator.Default.TryTranslate(text, out var translatedText))
            {
                text = translatedText;
                __state = true;
            }
            else
            {
                __state = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Utage.TextData), "CreateTextParser")]
        private static void CreateTextParser_Postfix(string text, TextParserBase __result, bool __state)
        {
            if (__state || _seen.Contains(text)) return;

            // If the text was not instantly translated, do it asynchronously and manually set the result and update the UI
            AutoTranslator.Default.TranslateAsync(text, result =>
            {
                if (!result.Succeeded || string.IsNullOrWhiteSpace(result.TranslatedText)) return;

                try
                {
                    // set result to protected string originalText;
                    var textParserType = __result.GetType();
                    var originalTextField = textParserType.GetField("originalText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                            ?? throw new System.Exception("Could not find field 'originalText' in TextParserBase");
                    originalTextField.SetValue(__result, result.TranslatedText);

                    //call protected virtual void Parse()
                    var parseMethod = textParserType.GetMethod("Parse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                      ?? throw new System.Exception("Could not find method 'Parse' in TextParserBase");
                    parseMethod.Invoke(__result, null);

                    ThreadingHelper.Instance.StartSyncInvoke(() =>
                    {
                        try
                        {
                            var advPage = FindObjectOfType<AdvPage>();

                            var advPageType = advPage.GetType();
                            var currentTextLengthMaxField = advPageType.GetProperty(nameof(AdvPage.CurrentTextLengthMax),
                                                                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            currentTextLengthMaxField.SetValue(advPage, result.TranslatedText.Length, null);

                            advPage.RemakeText();

                            _seen.Add(text);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("[TranslatedAdvTextFix] Exception in StartSyncInvoke callback: " + ex);
                        }
                        finally
                        {
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[TranslatedAdvTextFix] Exception in TranslateAsync callback: " + ex);
                }
                finally
                {
                }
            });
        }
    }
}
