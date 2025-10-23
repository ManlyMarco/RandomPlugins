using System.Collections;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoTranslator.IL2CPP.BruteForceFix
{
    [BepInPlugin(GUID, GUID, Version)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", "5.4")]
    public class AutoTranslatorBruteForceFixPlugin : BasePlugin
    {
        public const string GUID = "AutoTranslator.IL2CPP.BruteForceFix";
        public const string Version = Constants.Version;

        public override void Load()
        {
            AddComponent<AutoTranslatorBruteForceFixComponent>();
        }

        public sealed class AutoTranslatorBruteForceFixComponent : MonoBehaviour
        {
            private Il2CppSystem.Collections.IEnumerator Start()
            {
                return UpdateCo().WrapToIl2Cpp();

                IEnumerator UpdateCo()
                {
                    // Force trigger AT translation by re-assigning text properties
                    while (true)
                    {
                        // This seems to be running fast enough, the 0.5 delay is jarring when playing
                        //yield return new WaitForSeconds(0.5f);
                        yield return null;

                        foreach (var t in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            t.text = t.text;

                        yield return null;

                        foreach (var t in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            t.text = t.text;

                        yield return null;

                        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            t.text = t.text;

                        yield return null;

                        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            t.text = t.text;

                        //Console.WriteLine(sw.ElapsedMilliseconds);
                    }
                }
            }

        }
    }
}
