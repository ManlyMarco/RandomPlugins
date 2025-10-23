using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Timers;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TousatsuTwo_PhotoVideo_SaveFix
{
    [BepInPlugin(GUID, DisplayName, Version)]
    public class PhotoVideoSaveFixPlugin : BaseUnityPlugin
    {
        public const string GUID = nameof(TousatsuTwo_PhotoVideo_SaveFix);
        public const string DisplayName = Constants.Name;
        public const string Version = Constants.Version;

        private void Awake()
        {
            _saveTimer = new Timer();
            _saveTimer.Interval = 2000;
            _saveTimer.AutoReset = false;
            _saveTimer.Elapsed += (sender, args) => Save();
            _saveTimer.Enabled = false;

            Load();

            Harmony.CreateAndPatchAll(typeof(PhotoVideoSaveFixPlugin));
        }

        private readonly string _savePath = Path.Combine(Paths.GameRootPath, "GameSave_PhotoVideoSaveFixPlugin.bin");
        private static Dictionary<string, object> _saveData = new Dictionary<string, object>();

        private static Timer _saveTimer;

        private static void OnSaveChanged()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void Save()
        {
            try
            {
                lock (_savePath)
                    using (var stream = new FileStream(_savePath, FileMode.Create))
                        new BinaryFormatter().Serialize(stream, _saveData);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Logger.LogMessage("Failed to save, progress might be lost! Consider removing TousatsuTwo_PhotoVideo_SaveFix");
            }
        }

        private void Load()
        {
            try
            {
                lock (_savePath)
                {
                    if (File.Exists(_savePath))
                        using (var stream = File.OpenRead(_savePath))
                            _saveData = (Dictionary<string, object>)new BinaryFormatter().Deserialize(stream);
                    else
                        _saveData = new Dictionary<string, object>();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                _saveData = new Dictionary<string, object>();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.HasKey))]
        private static bool PlayerPrefsHasKeyPatch(string key, ref bool __result)
        {
            if (key.StartsWith("SAVE_"))
            {
                __result = _saveData.ContainsKey(key);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.GetFloat), typeof(string), typeof(float))]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.GetInt), typeof(string), typeof(int))]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.GetString), typeof(string), typeof(string))]
        private static bool PlayerPrefsGetPatch(string key, object defaultValue, ref object __result)
        {
            if (key.StartsWith("SAVE_"))
            {
                if (_saveData.TryGetValue(key, out var value))
                {
                    __result = value;
                    return false;
                }
                else
                {
                    __result = defaultValue;
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.GetFloat), typeof(string))]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.GetInt), typeof(string))]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.GetString), typeof(string))]
        private static bool PlayerPrefsGetPatch2(string key, ref object __result)
        {
            if (key.StartsWith("SAVE_"))
            {
                if (_saveData.TryGetValue(key, out var value))
                {
                    __result = value;
                    return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.SetFloat))]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.SetInt))]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.SetString))]
        private static bool PlayerPrefsPatch(string key, object value)
        {
            if (key.StartsWith("SAVE_"))
            {
                _saveData[key] = value;
                OnSaveChanged();
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.DeleteAll))]
        private static bool PlayerPrefsDeleteAllPatch()
        {
            _saveData.Clear();
            OnSaveChanged();
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.DeleteKey))]
        private static void PlayerPrefsDeleteKeyPatch(string key)
        {
            _saveData.Remove(key);
            OnSaveChanged();
        }
    }
}
