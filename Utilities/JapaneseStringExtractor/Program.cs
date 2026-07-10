using System;
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

            var textStrings = textFiles.Length > 0 ? ExtractFromFiles(textFiles) : new string[0];
            var assemblyStrings = assemblyFiles.SelectMany(f => ExtractFromAssembly(f)).ToArray();

            return textStrings.Concat(assemblyStrings).Distinct().ToArray();
        }

        private static string[] ExtractFromFiles(FileInfo[] files)
        {
            var strings = files
                          .OrderBy(x => x.Name)
                          .AsParallel().AsOrdered()
                          .SelectMany(x =>
                          {
                              Console.WriteLine("| Reading " + x.Name);
                              return File.ReadAllLines(x.FullName, UTF8Encoding.UTF8);
                          })
                          .SelectMany(x =>
                          {
                              var chars = x.Where(c =>
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

                              return x.Split(chars, StringSplitOptions.RemoveEmptyEntries);
                          });

            return strings.Where(x => x.Any(IsJapaneseChar) && !x.Contains('/') && !x.Contains('\\') && !x.Contains('_') && !x.Contains('=') && !x.Contains('�'))
                          .Select(x => x.Trim())
                          .Distinct() // todo: add an option to preserve duplicates
                          .ToArray();
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

            return strings.Where(x => x.Any(IsJapaneseChar) && !x.Contains('/') && !x.Contains('\\') && !x.Contains('_') && !x.Contains('=') && !x.Contains('�'))
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
                        var resourceStrings = ExtractStringsFromBytes(data);
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
                Console.WriteLine("| Performing dumb extraction from " + file.Name);
                var bytes = File.ReadAllBytes(file.FullName);
                return ExtractStringsFromBytes(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| Failed dumb extraction: {ex.Message}");
                return new string[0];
            }
        }

        private static string[] ExtractStringsFromBytes(byte[] bytes)
        {
            var strings = new List<string>();
            var currentString = new StringBuilder();

            // Extract null-terminated UTF-8 and ASCII strings
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];

                // Check for printable ASCII or start of UTF-8 sequence
                if ((b >= 0x20 && b <= 0x7E) || b >= 0x80)
                {
                    currentString.Append((char)b);
                }
                else if (currentString.Length > 0)
                {
                    // End of string found
                    var str = currentString.ToString();
                    if (str.Length >= 2) // Minimum length filter
                    {
                        // Try to decode as UTF-8
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
                            // If UTF-8 decode fails, use the original string
                            strings.Add(str);
                        }
                    }
                    currentString.Clear();
                }
            }

            // Also try to find UTF-16 (Unicode) strings
            for (int i = 0; i < bytes.Length - 1; i += 2)
            {
                try
                {
                    var c = (char)(bytes[i] | (bytes[i + 1] << 8));
                    if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c) || c >= 0x3000)
                    {
                        currentString.Append(c);
                    }
                    else if (currentString.Length > 0)
                    {
                        var str = currentString.ToString().Trim();
                        if (str.Length >= 2)
                        {
                            strings.Add(str);
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
                            strings.Add(str);
                        }
                        currentString.Clear();
                    }
                }
            }

            return strings.Where(x => x.Any(IsJapaneseChar) && !x.Contains('/') && !x.Contains('\\') && !x.Contains('_') && !x.Contains('=') && !x.Contains('\ufffd'))
                          .Select(x => x.Trim())
                          .Distinct()
                          .ToArray();
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
    }
}
