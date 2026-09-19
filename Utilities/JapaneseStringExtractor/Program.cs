using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace JapaneseStringExtractor
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var path = args[i];
                if (File.Exists(path) || Directory.Exists(path))
                    continue;

                Console.Error.WriteLine($"Error: Path not found: {path}");
                args = args.Where(x => !string.Equals(x, path, StringComparison.OrdinalIgnoreCase)).ToArray();
                i = -1;
            }

            if (args.Length != 0)
            {
                var dumpDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])) ?? "", "_StringDump");
                Directory.CreateDirectory(dumpDirectory);

                int total = args.Length;
                for (int i = 0; i < total; i++)
                {
                    var path = args[i];
                    Console.WriteLine($"[{i + 1}/{total}] Processing: {path}");
                    if (File.Exists(path))
                    {
                        var fileInfo = new FileInfo(path);
                        string[] strings;

                        // Check if this is a .NET assembly
                        if (fileInfo.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                            fileInfo.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            strings = ExtractFromAssembly(fileInfo);
                        }
                        else
                        {
                            strings = ExtractFromFiles(new[] { fileInfo });
                        }

                        var outPath = Path.Combine(dumpDirectory, fileInfo.Name + "_dump.txt");
                        Console.WriteLine($"L Writing {strings.Length} strings to {outPath}");
                        Console.WriteLine();
                        File.WriteAllLines(outPath, strings);
                    }
                    else if (Directory.Exists(path))
                    {
                        var dirInfo = new DirectoryInfo(path);
                        {
                            var strings = ExtractFromDirectory(dirInfo);
                            var outPath = Path.Combine(dumpDirectory, dirInfo.Name + "_files_dump.txt");
                            Console.WriteLine($"L Writing {strings.Length} strings to {outPath}");
                            Console.WriteLine();
                            File.WriteAllLines(outPath, strings);
                        }

                        var dirs = dirInfo.GetDirectories().OrderBy(x => x.Name).ToArray();
                        for (var i2 = 0; i2 < dirs.Length; i2++)
                        {
                            var subDir = dirs[i2];
                            Console.WriteLine($"[{i + 1}/{total} | {i2 + 1}/{dirs.Length}] Processing subdirectory: {subDir.Name}");
                            var strings = ExtractFromFilesInDirectory(subDir);
                            var outPath = Path.Combine(dumpDirectory, $"{dirInfo.Name}_{subDir.Name}_dump.txt");
                            Console.WriteLine($"L Writing {strings.Length} strings to {outPath}");
                            Console.WriteLine();
                            File.WriteAllLines(outPath, strings);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Path not found: {path}");
                    }
                }
            }
            else
            {
                Console.Error.WriteLine("Error: No file or directory paths provided.\nUsage: JapaneseStringExtractor.exe <file_or_directory_path> [more_paths...]");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static string[] ExtractFromDirectory(DirectoryInfo directory)
        {
            return ExtractFromFilesInDirectory(directory, SearchOption.TopDirectoryOnly);
        }

        private static string[] ExtractFromFilesInDirectory(DirectoryInfo directory, SearchOption searchOption = SearchOption.AllDirectories)
        {
            var allFiles = directory.GetFiles("*.*", searchOption);
            var textFiles = allFiles.Where(f => !f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                                                !f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)).ToArray();
            var assemblyFiles = allFiles.Where(f => f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                                                    f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)).ToArray();

            var textStrings = textFiles.Length > 0 ? ExtractFromFiles(textFiles) : Array.Empty<string>();
            var assemblyStrings = assemblyFiles.SelectMany(ExtractFromAssembly).ToArray();

            return textStrings.Concat(assemblyStrings).Distinct().ToArray();
        }

        private static string[] ExtractFromFiles(FileInfo[] files)
        {
            var resultSet = new HashSet<string>();

            foreach (var fileInfo in files.OrderBy(x => x.Name))
            {
                long fileSize = fileInfo.Length;
                int lastPercent = -1;
                int foundLines = 0;

                Console.Write($"| Reading {fileInfo.Name}");

                using (var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var chars = line.Where(c =>
                        {
                            if (char.IsControl(c) || c == ',') return true;
                            var uc = char.GetUnicodeCategory(c);

                            if (uc == UnicodeCategory.Surrogate)
                            {
                                // Unpaired surrogate, like  "😵"[0] + "A" or  "😵"[1] + "A"
                                return true;
                            }
                            else if (uc == UnicodeCategory.OtherNotAssigned /*|| uc == UnicodeCategory.OtherSymbol*/)
                            {
                                // \uF000 or \U00030000
                                return true;
                            }

                            return false;
                        }).Distinct().ToArray();

                        var splitStrings = line.Split(chars, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var str in splitStrings)
                        {
                            var trimmed = str.Trim();
                            if (trimmed.Length > 1 && !trimmed.Contains('/') && !trimmed.Contains('\\') && !trimmed.Contains('_') && !trimmed.Contains('=') && !trimmed.Contains('�') && IsValidJapaneseString(trimmed))
                            {
                                resultSet.Add(trimmed);
                                foundLines++;
                            }
                        }

                        // Update progress periodically
                        if (fileSize > 0)
                        {
                            long currentPosition = stream.Position;
                            int currentPercent = (int)((currentPosition * 100) / fileSize);
                            currentPercent = Math.Min(currentPercent, 99); // Cap at 99% until completion
                            if (currentPercent != lastPercent && currentPercent % 5 == 0)
                            {
                                lastPercent = currentPercent;
                                Console.Write($"\r| Reading {fileInfo.Name} {currentPercent}% (found {foundLines} lines)");
                            }
                        }
                    }
                }

                Console.WriteLine($"\r| Reading {fileInfo.Name} 100% (found {foundLines} lines)");
            }

            return resultSet.ToArray();
        }

        private static string[] ExtractFromAssembly(FileInfo assemblyFile)
        {
            var strings = new List<string>();

            try
            {
                Console.WriteLine("| Reading assembly " + assemblyFile.Name);

                var readerParameters = new ReaderParameters { ReadSymbols = false };
                using (var assembly = AssemblyDefinition.ReadAssembly(assemblyFile.FullName, readerParameters))
                {
                    foreach (var module in assembly.Modules)
                    {
                        // Extract strings from types and methods
                        foreach (var type in module.Types)
                        {
                            ExtractStringsFromType(type, strings);
                        }

                        // Extract strings from embedded resources
                        if (module.HasResources)
                        {
                            ExtractStringsFromResources(module, strings);
                        }
                    }
                }
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine($"| {assemblyFile.Name} is not a valid .NET assembly, falling back to dumb extraction");
                return ExtractFromBinaryFile(assemblyFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| Error reading {assemblyFile.Name}: {ex.Message}, falling back to dumb extraction");
                return ExtractFromBinaryFile(assemblyFile);
            }

            return strings.Where(x => IsValidJapaneseString(x) && !x.Contains('/') && !x.Contains('\\') && !x.Contains('_') && !x.Contains('=') && !x.Contains('�'))
                          .Select(x => x.Trim())
                          .Distinct()
                          .ToArray();
        }

        private static void ExtractStringsFromType(TypeDefinition type, List<string> strings)
        {
            // Extract strings from type attributes
            if (type.HasCustomAttributes)
            {
                ExtractStringsFromAttributes(type.CustomAttributes, strings);
            }

            // Extract strings from fields
            foreach (var field in type.Fields)
            {
                if (field.HasCustomAttributes)
                {
                    ExtractStringsFromAttributes(field.CustomAttributes, strings);
                }
            }

            // Extract strings from properties
            foreach (var property in type.Properties)
            {
                if (property.HasCustomAttributes)
                {
                    ExtractStringsFromAttributes(property.CustomAttributes, strings);
                }
            }

            // Extract strings from methods and their attributes
            foreach (var method in type.Methods)
            {
                if (method.HasCustomAttributes)
                {
                    ExtractStringsFromAttributes(method.CustomAttributes, strings);
                }

                if (method.HasParameters)
                {
                    foreach (var param in method.Parameters)
                    {
                        if (param.HasCustomAttributes)
                        {
                            ExtractStringsFromAttributes(param.CustomAttributes, strings);
                        }
                    }
                }

                if (method.HasBody)
                {
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string str)
                        {
                            if (!string.IsNullOrWhiteSpace(str))
                            {
                                strings.Add(str);
                            }
                        }
                    }
                }
            }

            // Extract strings from events
            foreach (var evt in type.Events)
            {
                if (evt.HasCustomAttributes)
                {
                    ExtractStringsFromAttributes(evt.CustomAttributes, strings);
                }
            }

            // Recursively process nested types
            foreach (var nestedType in type.NestedTypes)
            {
                ExtractStringsFromType(nestedType, strings);
            }
        }

        private static void ExtractStringsFromAttributes(Mono.Collections.Generic.Collection<CustomAttribute> attributes, List<string> strings)
        {
            foreach (var attr in attributes)
            {
                // Check constructor arguments
                if (attr.HasConstructorArguments)
                {
                    foreach (var arg in attr.ConstructorArguments)
                    {
                        ExtractStringFromAttributeArgument(arg, strings);
                    }
                }

                // Check named properties
                if (attr.HasProperties)
                {
                    foreach (var prop in attr.Properties)
                    {
                        ExtractStringFromAttributeArgument(prop.Argument, strings);
                    }
                }

                // Check named fields
                if (attr.HasFields)
                {
                    foreach (var field in attr.Fields)
                    {
                        ExtractStringFromAttributeArgument(field.Argument, strings);
                    }
                }
            }
        }

        private static void ExtractStringFromAttributeArgument(CustomAttributeArgument arg, List<string> strings)
        {
            if (arg.Value is string str && !string.IsNullOrWhiteSpace(str))
            {
                strings.Add(str);
            }
            else if (arg.Value is CustomAttributeArgument[] array)
            {
                foreach (var item in array)
                {
                    ExtractStringFromAttributeArgument(item, strings);
                }
            }
        }

        private static void ExtractStringsFromResources(ModuleDefinition module, List<string> strings)
        {
            foreach (var resource in module.Resources)
            {
                if (resource is EmbeddedResource embeddedResource)
                {
                    try
                    {
                        Console.WriteLine("| Extracting from embedded resource: " + resource.Name);
                        var data = embeddedResource.GetResourceData();

                        // Try to extract readable strings from the resource data
                        int overlapLen = 0;
                        var resourceStrings = ExtractStringsFromByteChunk(data, data.Length, null, ref overlapLen);
                        strings.AddRange(resourceStrings);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"| Failed to extract from resource {resource.Name}: {ex.Message}");
                    }
                }
            }
        }

        private static string[] ExtractFromBinaryFile(FileInfo file)
        {
            try
            {
                Console.Write($"| Performing dumb extraction from {file.Name}");
                Console.Out.Flush();

                var resultSet = new HashSet<string>();
                const int bufferSize = 1024 * 1024; // 1MB chunks
                byte[] buffer = new byte[bufferSize];
                byte[] overlapBuffer = new byte[10]; // To handle strings split across chunk boundaries
                int overlapLen = 0;
                long fileSize = file.Length;
                int lastPercent = -1;

                using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                {
                    int chunkBytesRead;
                    while ((chunkBytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                    {
                        var chunkStrings = ExtractStringsFromByteChunk(buffer, chunkBytesRead, overlapBuffer, ref overlapLen);
                        foreach (var str in chunkStrings)
                        {
                            if (IsValidJapaneseString(str) && !str.Contains('/') && !str.Contains('\\') && !str.Contains('_') && !str.Contains('=') && !str.Contains('\ufffd'))
                            {
                                resultSet.Add(str);
                            }
                        }

                        // Update progress
                        if (fileSize > 0)
                        {
                            long currentPosition = stream.Position;
                            int currentPercent = (int)((currentPosition * 100) / fileSize);
                            currentPercent = Math.Min(currentPercent, 99); // Cap at 99% until completion
                            if (currentPercent != lastPercent && currentPercent % 5 == 0)
                            {
                                lastPercent = currentPercent;
                                Console.Write($"\r| Performing dumb extraction from {file.Name} {currentPercent}% (found {resultSet.Count} lines)");
                                Console.Out.Flush();
                            }
                        }
                    }

                    // Process any remaining overlap buffer
                    if (overlapLen > 0)
                    {
                        var finalStrings = ExtractStringsFromByteChunk(overlapBuffer, overlapLen, null, ref overlapLen);
                        foreach (var str in finalStrings)
                        {
                            if (IsValidJapaneseString(str) && !str.Contains('/') && !str.Contains('\\') && !str.Contains('_') && !str.Contains('=') && !str.Contains('\ufffd'))
                            {
                                resultSet.Add(str);
                            }
                        }
                    }
                }

                Console.WriteLine($"\r| Performing dumb extraction from {file.Name} 100% (found {resultSet.Count} lines)");
                return resultSet.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| Failed dumb extraction: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private static List<string> ExtractStringsFromByteChunk(byte[] buffer, int length, byte[] overlapBuffer, ref int overlapLen)
        {
            var strings = new List<string>();
            var currentString = new StringBuilder();
            int processLen = length;

            // Process buffer
            for (int i = 0; i < processLen; i++)
            {
                byte b = buffer[i];

                // Check for printable ASCII or start of UTF-8 sequence
                if ((b >= 0x20 && b <= 0x7E) || b >= 0x80)
                {
                    currentString.Append((char)b);
                }
                else if (currentString.Length > 0)
                {
                    // End of string found
                    var str = currentString.ToString();
                    if (str.Length >= 2)
                    {
                        try
                        {
                            var utf8Bytes = Encoding.Default.GetBytes(str);
                            var decoded = Encoding.UTF8.GetString(utf8Bytes);
                            if (!string.IsNullOrWhiteSpace(decoded))
                            {
                                strings.Add(decoded);
                            }
                        }
                        catch
                        {
                            strings.Add(str);
                        }
                    }
                    currentString.Clear();
                }
            }

            // UTF-16 extraction with overlap handling
            int utf16Start = (overlapLen > 0) ? -overlapLen : 0;
            int utf16End = processLen - 1;
            if (utf16End % 2 == 0) utf16End--;

            for (int i = utf16Start; i < utf16End; i += 2)
            {
                byte b1, b2;
                if (i < 0)
                {
                    b1 = overlapBuffer[overlapLen + i];
                    b2 = (i + 1 < 0) ? overlapBuffer[overlapLen + i + 1] : buffer[0];
                }
                else
                {
                    b1 = buffer[i];
                    b2 = (i + 1 < processLen) ? buffer[i + 1] : (overlapBuffer != null ? overlapBuffer[0] : (byte)0);
                }

                try
                {
                    var c = (char)(b1 | (b2 << 8));
                    if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c) || c >= 0x3000)
                    {
                        currentString.Append(c);
                    }
                    else if (currentString.Length > 0)
                    {
                        var str = currentString.ToString().Trim();
                        if (str.Length >= 2)
                        {
                            var cleanedStr = RemoveUnpairedSurrogates(str);
                            if (cleanedStr.Length >= 2)
                            {
                                strings.Add(cleanedStr);
                            }
                        }
                        currentString.Clear();
                    }
                }
                catch
                {
                    if (currentString.Length > 0)
                    {
                        var str = currentString.ToString().Trim();
                        if (str.Length >= 2)
                        {
                            var cleanedStr = RemoveUnpairedSurrogates(str);
                            if (cleanedStr.Length >= 2)
                            {
                                strings.Add(cleanedStr);
                            }
                        }
                        currentString.Clear();
                    }
                }
            }

            // Save overlap for next chunk
            if (overlapBuffer != null && processLen > 0)
            {
                overlapLen = Math.Min(10, processLen);
                Array.Copy(buffer, processLen - overlapLen, overlapBuffer, 0, overlapLen);
            }

            return strings;
        }

        private static string RemoveUnpairedSurrogates(string str)
        {
            var result = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (char.IsSurrogate(c))
                {
                    if (char.IsHighSurrogate(c) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
                    {
                        // Valid surrogate pair, keep both characters
                        result.Append(c);
                        result.Append(str[i + 1]);
                        i++; // Skip the low surrogate in the next iteration
                    }
                    // Skip unpaired surrogates
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        private static bool IsJapaneseChar(char c)
        {
            // Unicode Kanji Table:
            // http://www.rikai.com/library/kanjitables/kanji_codes.unicode.shtml
            return (c >= '\u3021' && c <= '\u3029') // kana-like symbols
                   || (c >= '\u3031' && c <= '\u3035') // kana-like symbols
                   || (c >= '\u3041' && c <= '\u3096') // hiragana
                   || (c >= '\u30a1' && c <= '\u30fa') // katakana
                   || (c >= '\uff66' && c <= '\uff9d') // half-width katakana
                   || (c >= '\u4e00' && c <= '\u9faf') // CJK unifed ideographs - Common and uncommon kanji
                   || (c >= '\u3400' && c <= '\u4dbf') // CJK unified ideographs Extension A - Rare kanji ( 3400 - 4dbf)
                   || (c >= '\uf900' && c <= '\ufaff') // CJK Compatibility Ideographs
                   || (c == '、' || c == '。');
        }

        private static bool IsValidJapaneseString(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;

            // Count Japanese characters and other character types
            int japaneseCount = 0;
            int latinCount = 0;
            int digitCount = 0;
            int controlOrInvalidCount = 0;

            foreach (char c in str)
            {
                if (IsJapaneseChar(c))
                {
                    japaneseCount++;
                }
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    latinCount++;
                }
                else if (c >= '0' && c <= '9')
                {
                    digitCount++;
                }
                else if (char.IsWhiteSpace(c) || c == '、' || c == '。' || c == '(' || c == ')'
                         || c == '（' || c == '）' || c == '『' || c == '』' || c == '「' || c == '」'
                         || c == '【' || c == '】' || c == '…' || c == '・' || c == '：' || c == '！'
                         || c == '？' || c == '，' || c == '；')
                {
                    // Allow common punctuation and whitespace
                }
                else if (char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.OtherNotAssigned
                         || char.GetUnicodeCategory(c) == UnicodeCategory.Surrogate
                         || char.GetUnicodeCategory(c) == UnicodeCategory.PrivateUse)
                {
                    controlOrInvalidCount++;
                }
                else if (char.IsSymbol(c) && c > 127)
                {
                    // Allow extended symbols (likely Japanese-related)
                }
                else
                {
                    // Unknown/unexpected character - treat as noise
                    latinCount++;
                }
            }

            // String must have meaningful Japanese content
            if (japaneseCount == 0)
                return false;

            // Reject if it has too many control/invalid characters
            if (controlOrInvalidCount > 0)
                return false;

            // Conservative filtering: if mostly Latin/digits with few Japanese chars, reject
            int noiseCount = latinCount + digitCount;

            // Allow short strings only if they're mostly Japanese
            if (str.Length < 3 && noiseCount > japaneseCount)
                return false;

            // For longer strings, don't allow more than 1-2 Latin chars mixed in unless there are many Japanese chars
            if (japaneseCount < 2 && noiseCount > 0)
                return false;

            return true;
        }
    }
}
