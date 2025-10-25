using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace JapaneseStringExtractor
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: No file or directory paths provided.\nUsage: JapaneseStringExtractor.exe <file_or_directory_path> [more_paths...]");
                return;
            }

            var dumpDirectory = Path.Combine(Environment.CurrentDirectory, "StringDump");
            Directory.CreateDirectory(dumpDirectory);

            int total = args.Length;
            for (int i = 0; i < total; i++)
            {
                var path = args[i];
                Console.WriteLine($"[{i + 1}/{total}] Processing: {path}");
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    var strings = ExtractFromFiles(new[] { fileInfo });
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
                        var strings = ExtractFromFiles(subDir.GetFiles("*.*", SearchOption.AllDirectories));
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

        private static string[] ExtractFromDirectory(DirectoryInfo directory)
        {
            return ExtractFromFiles(directory.GetFiles("*.*", SearchOption.TopDirectoryOnly));
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
