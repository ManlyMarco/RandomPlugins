using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace TranslationFileMergeUtility
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 2 && IsTxtFile(args[0]) && IsTxtFile(args[1]))
            {
                MergeIntoTranslationFile(args[0], args[1]);
            }
            else if (args.Length == 1 && IsTxtFile(args[0]))
            {
                SplitTranslationFile(args[0]);
            }
            else
            {
                Console.WriteLine("Usage:\n  Merge: TranslationFileMergeUtility.exe <file1.txt> <file2.txt>\n  Split: TranslationFileMergeUtility.exe <file.txt>");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static bool IsTxtFile(string path)
        {
            return Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase);
        }

        private static void MergeIntoTranslationFile(string file1, string file2)
        {
            Console.WriteLine($"Merging files:\n  Original:  {file1}\n  Translated:  {file2}");

            var jp = new List<string>(File.ReadAllLines(file1));
            var en = new List<string>(File.ReadAllLines(file2));

            // Remove trailing empty lines
            while (jp.Count > 0 && string.IsNullOrWhiteSpace(jp[jp.Count - 1])) jp.RemoveAt(jp.Count - 1);
            while (en.Count > 0 && string.IsNullOrWhiteSpace(en[en.Count - 1])) en.RemoveAt(en.Count - 1);

            if (jp.Count != en.Count)
                Console.WriteLine($"WARNING: File line counts do not match!\n{file1}: {jp.Count}\n{file2}: {en.Count}");

            var output = new List<string>(jp.Count);
            int minLen = Math.Min(jp.Count, en.Count);
            for (int i = 0; i < minLen; i++)
            {
                var j = jp[i];
                var e = en[i];
                if (string.IsNullOrEmpty(j)) continue;
                if (string.IsNullOrEmpty(e))
                {
                    Console.WriteLine($"INVALID LINE: {j} -> <empty>");
                    output.Add("//" + MakeLine(j, ""));
                }
                else
                {
                    output.Add(MakeLine(j, e));
                }
            }
            var outPath = Path.ChangeExtension(file1, null) + "_TL.txt";
            File.WriteAllLines(outPath, output);
            Console.WriteLine($"Merged file with {output.Count} lines written to: {outPath}");
        }

        private static void SplitTranslationFile(string file)
        {
            Console.WriteLine($"Splitting translation file: {file}");

            var lines = File.ReadAllLines(file);
            var originals = new List<string>();
            var translations = new List<string>();
            foreach (var line in lines)
            {
                var split = SplitLine(line);
                if (string.IsNullOrEmpty(split.Item1)) continue;
                originals.Add(split.Item1);
                translations.Add(split.Item2);
            }
            var origPath = Path.ChangeExtension(file, null) + "_Original.txt";
            var transPath = Path.ChangeExtension(file, null) + "_Translation.txt";
            File.WriteAllLines(origPath, originals);
            File.WriteAllLines(transPath, translations);
            Console.WriteLine($"Original text written to: {origPath}");
            Console.WriteLine($"Translated text written to: {transPath}");
        }

        // Helper: Split line on first unescaped =
        private static Tuple<string, string> SplitLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return Tuple.Create<string, string>(null, null);
            int idx = -1;
            bool escape = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\') escape = !escape;
                else if (line[i] == '=' && !escape)
                {
                    idx = i;
                    break;
                }
                else escape = false;
            }
            if (idx == -1) return Tuple.Create(Unescape(line), "");
            var orig = Unescape(line.Substring(0, idx));
            var trans = Unescape(line.Substring(idx + 1));
            return Tuple.Create(orig, trans);
        }

        // Helper: Make line with escaping
        private static string MakeLine(string orig, string trans)
        {
            return Escape(orig) + "=" + Escape(trans);
        }

        private static string Escape(string s)
        {
            return s.Replace(@"\", @"\\").Replace("=", @"\=");
        }
        private static string Unescape(string s)
        {
            return s.Replace(@"\\", @"\").Replace(@"\=", "=");
        }
    }
}
