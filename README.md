# RandomPlugins
A collection of BepInEx plugins and utilities that didn't deserve to be put into a separate repository.

## Plugins
Each of the plugins should be placed in the `BepInEx\plugins` folder of the target game. Some plugins may have additional installation instructions in their respective folders.

Use the latest version of BepInEx 5 for games that use Mono, and BepInEx 6 for IL2CPP games.

The plugins are made for the latest version of their respective games (at that time) unless stated otherwise. Compatibility with future game updates is likely but not guaranteed.

### Universal Plugins
- **AutoTranslator.IL2CPP.BruteForceFix**: Fixes issues with XUnity AutoTranslator failing to automatically translate some text components in IL2CPP Unity games (when the translation works after pressing Alt+T twice). It forces all text components to be periodically re-checked for text to be translated, ensuring they are translated right away. Has a small performance penalty.

### Game-specific Plugins
- **AGH_Tweaks**: Adds translation and debug tweaks for the game "Houkago Rinkan Chuudoku" shortened to AGH. This plugin requires the English translation + uncensor mod (the custom Assembly-CSharp.dll file) to be installed. It adds some improvements to the English translation while also allowing selectively disabling English translation and H scene subtitles. It also has an option to add a debug mode button to title screen.
- **WidescreenFix_DatsuiJanken**: Adds support for widescreen and custom resolutions in "Datsui Janken (RJ435105)", fixing UI scaling and resolution selection issues on ultrawide displays.
- **TousatsuTwo_PhotoVideo_SaveFix**: Fixes extreme save/load inefficiency for photo and video data in "Tousatsu Two (RJ01100703)".
- **妹と過ごす1ヵ月間_TranslatedAdvTextFix**: Fixes translated ADV text being cut off in "妹と過ごす1ヵ月間" (Imouto to Sugosu 1-Kagetsukan, posted on ci-en) by periodically forcing full text reveal.

## Utilities
These are simple command-line tools that can be run from anywhere (unless specified otherwise). Check their source code for usage instructions.

- **BepInDependencyChecker**: Command-line tool to analyze BepInEx plugin assemblies for missing, unnecessary, or mismatched BepInDependency attributes. The purpose is to catch potential unpredictable dependency issues where a plugin assembly is force-loaded before BepInEx gets to loading it, causing undefined behaviour (the wrong version of the plugin to be loaded or the loading process may fail). Best to use on a full `BepInEx\plugins` folder to get an accurate report.
- **CardImageReplacer**: Tool for replacing first PNG data stream in file without affecting other data.
- **HoneyComeSteamPassthrough**: Starts InitSetting.exe or HoneyCome.exe if the former is missing. Necessary for Steam to be able to start the game after it is converted to the global version.
