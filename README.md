# Random Plugins and Utilities
A collection of BepInEx plugins and various utilities that didn't deserve to be put into a separate repository.

## Plugins
Each of the plugins should be placed in the `BepInEx\plugins` folder of the target game. Some plugins may have additional installation instructions in their respective folders.

Use the latest version of BepInEx 5 for games that use Mono, and BepInEx 6 for IL2CPP games.

The plugins are made for the latest version of their respective games (at that time) unless stated otherwise. Compatibility with future game updates is likely but not guaranteed.

### Universal Plugins
- **AutoTranslator.IL2CPP.BruteForceFix**: Fixes issues with XUnity AutoTranslator failing to automatically translate some text components in IL2CPP Unity games (when the translation works after pressing Alt+T twice). It forces all text components to be periodically re-checked for text to be translated, ensuring they are translated right away. Has a small performance penalty. Requires XUnity AutoTranslator.

### Game-specific Plugins
- **AGH_Tweaks**: Adds translation and debug tweaks for "Houkago Rinkan Chuudoku" (AGH). Requires the English translation + uncensor mod (custom Assembly-CSharp.dll). Features include small improvements to English translation, options to disable translation/subtitles individually, and a debug mode button on the title screen. Requires the modded Assembly-CSharp.dll with English translations.
- **WidescreenFix_DatsuiJanken**: Enables widescreen and custom resolutions in "Datsui Janken (RJ435105)", fixing UI scaling and resolution selection for ultrawide displays.
- **TousatsuTwo_PhotoVideo_SaveFix**: Optimizes save/load performance for photo and video data in "Tousatsu Two (RJ01100703)" by fixing inefficient serialization. !No longer required in the latest version of the game!
- **妹と過ごす1ヵ月間_TranslatedAdvTextFix**: Fixes translated ADV text being cut off in "妹と過ごす1ヵ月間" (Imouto to Sugosu 1-Kagetsukan) by periodically forcing a full text reveal.

## Utilities
These are simple command-line tools that can be run from anywhere (unless specified otherwise). Check their source code for usage instructions. All utilities require .NET Framework 4.6 or later.

### AutoTranslator-related Utilities
- **JapaneseStringExtractor**: Extracts Japanese strings from files or directories, outputting detected text to a dump file for translation or analysis. Usage: `JapaneseStringExtractor.exe <file_or_directory_path> [more_paths...]`. Outputs results to a `StringDump` folder.
- **TranslationFileMergeUtility**: Merges original and translated text files into a translation file, or splits a translation file into original and translated components. Usage: `TranslationFileMergeUtility.exe <file1.txt> <file2.txt>` to merge, or `TranslationFileMergeUtility.exe <file.txt>` to split.

### Other Utilities
- **BepInDependencyChecker**: Analyzes BepInEx plugin assemblies for missing, unnecessary, or mismatched BepInDependency attributes. Helps detect dependency issues that may cause plugins to load incorrectly. Usage: `BepInDependencyChecker.exe <dll_or_directory> [-u] [-m]` where `-u` shows unnecessary dependencies and `-m` shows matching dependencies.
- **CardImageReplacer**: Replaces the first PNG image data stream in a file (such as a character card) with a new image, preserving other data. Usage: `CardImageReplacer.exe <card.png> <replacement.png> <output.png>`. If arguments are omitted, a GUI will prompt for files.
- **HoneyComeSteamPassthrough**: Allows Steam to launch "HoneyCome Come Come Party" after conversion to the global version ("HoneyCome") by starting `InitSetting.exe` (or `HoneyCome.exe` if the former is missing). Place in the game root folder.
