using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OllamaFileTranslator;

internal class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var exePath = AppContext.BaseDirectory;
        var configPath = Path.Combine(exePath, "ollama-config.yaml");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Error: Config file not found: {configPath}");
            Console.Error.WriteLine("Please place ollama-config.yaml in the same directory as the executable.");
            return;
        }

        try
        {
            var translator = new Translator(configPath);
            await translator.Initialize();

            var filesToTranslate = new List<string>();

            // Collect all files to translate from arguments
            foreach (var path in args)
            {
                if (Directory.Exists(path))
                {
                    var txtFiles = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
                    filesToTranslate.AddRange(txtFiles);
                }
                else if (File.Exists(path))
                {
                    filesToTranslate.Add(path);
                }
                else
                {
                    Console.WriteLine($"Warning: Path not found: {path}");
                }
            }

            if (filesToTranslate.Count == 0)
            {
                Console.WriteLine("No files to translate.");
                return;
            }

            Console.WriteLine($"Found {filesToTranslate.Count} file(s) to translate.");

            foreach (var file in filesToTranslate)
            {
                Console.WriteLine($"\nTranslating: {file}");
                await translator.TranslateFile(file);
            }

            Console.WriteLine("\nTranslation complete!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: OllamaFileTranslator.exe <folder-or-file> [folder-or-file] ...");
        Console.WriteLine("");
        Console.WriteLine("Arguments:");
        Console.WriteLine("  folder-or-file       Path to a folder (searches all subdirectories) or individual file");
        Console.WriteLine("");
        Console.WriteLine("The configuration file 'ollama-config.yaml' must be in the same directory as the executable.");
        Console.WriteLine("");
        Console.WriteLine("Examples:");
        Console.WriteLine("  OllamaFileTranslator.exe E:\\dump");
        Console.WriteLine("  OllamaFileTranslator.exe E:\\file1.txt E:\\folder");
    }
}