using System.Collections;
using BepInEx;
using HarmonyLib;
using Naninovel.UI; // TODO: Needs a reference to the game assembly-csharp
using UnityEngine;

namespace TranslatedAdvTextFix
{
    [BepInPlugin(GUID, DisplayName, Version)]
    public class TranslatedAdvTextFixPlugin : BaseUnityPlugin
    {
        public const string Version = Constants.Version;
        public const string GUID = "TranslatedAdvTextFix";
        public const string DisplayName = Constants.Name;

        private void Start()
        {
            StartCoroutine(SlowUpdate());
        }

        private IEnumerator SlowUpdate()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.3f);

                foreach (var uiText in GameObject.FindObjectsOfType<RevealableUIText>())
                {
                    if (uiText.Revealing)
                    {
                        while (uiText.Revealing)
                        {
                            yield return new WaitForSecondsRealtime(0.1f);
                        }
                        yield return new WaitForSecondsRealtime(0.1f);
                    }

                    if (uiText.RevealProgress < 1f)
                    {
                        var field = Traverse.Create(uiText).Field("revealBehaviour");
                        if (field.GetValue() != null)
                            field.Method("RevealAll").GetValue();
                    }
                }
            }
        }
    }
}