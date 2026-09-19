using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OllamaFileTranslator;

public class Translator
{
    private readonly string _configPath;
    private readonly HttpClient _client;
    private OllamaConfig _config;
    private string _glossaryPrompt;
    private string _systemPrompt;

    public Translator(string configPath)
    {
        _configPath = configPath;
        _client = new HttpClient();
    }

    public Task Initialize()
    {
        var configContent = File.ReadAllText(_configPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _config = deserializer.Deserialize<OllamaConfig>(configContent);

        if (_config == null) throw new InvalidOperationException("Failed to deserialize config file");

        _systemPrompt = _config.SystemPrompt ?? "";
        _glossaryPrompt = _config.GlossaryPrompt ?? "";

        Console.WriteLine($"Loaded config from: {_configPath}");
        Console.WriteLine($"Model: {_config.Model}");
        Console.WriteLine($"URL: {_config.Url}");

        return Task.CompletedTask;
    }

    public async Task TranslateFile(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        Console.WriteLine($"Read {lines.Length} lines from file");

        var translatedLines = new string[lines.Length];
        var successCount = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            try
            {
                var translated = await TranslateLine(line);
                translatedLines[i] = EscapeLine(line) + "=" + EscapeLine(translated);
                successCount++;

                // Update progress message in-place
                var lineNumber = i + 1;
                var percentage = (int)(lineNumber * 100.0 / lines.Length);
                Console.Write($"\r  Translated {percentage}% ({lineNumber}/{lines.Length} lines)");
            }
            catch (Exception ex)
            {
                Console.Write($"\r  Error translating line {i + 1}: {ex.Message}\r\n");
                translatedLines[i] = EscapeLine(line) + "=" + EscapeLine("[Translation failed]");
            }
        }

        Console.WriteLine(); // New line after progress

        // Save translated file with _TL suffix
        var outputPath = GenerateOutputPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllLines(outputPath, translatedLines, Encoding.UTF8);

        Console.WriteLine($"Saved to: {outputPath}");
        Console.WriteLine($"Success: {successCount}/{lines.Length} lines translated");
    }

    private async Task<string> TranslateLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return "";

        var messages = new List<object>
        {
            new { role = "system", content = BuildSystemPrompt() },
            new { role = "user", content = line }
        };

        var requestBody = new
        {
            model = _config.Model,
            messages,
            stream = false
        };

        // Add model parameters
        var requestDict = new Dictionary<string, object>
        {
            { "model", _config.Model },
            { "messages", messages },
            { "stream", false }
        };

        if (_config.ModelParams != null)
            foreach (var param in _config.ModelParams)
                requestDict[param.Key] = param.Value;

        var json = JsonSerializer.Serialize(requestDict);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(_config.Url, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

        if (responseJson.TryGetProperty("message", out var messageProperty))
            if (messageProperty.TryGetProperty("content", out var contentProperty))
                return contentProperty.GetString() ?? "";

        return "";
    }

    private string BuildSystemPrompt()
    {
        var prompt = _systemPrompt;
        if (!string.IsNullOrEmpty(_glossaryPrompt)) prompt += "\n\n" + _glossaryPrompt;
        return prompt;
    }

    private string EscapeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";

        return line
            .Replace("\\", "\\\\")
            .Replace("=", "\\=");
    }

    private string GenerateOutputPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath);
        var filename = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);

        return Path.Combine(directory, filename + "_TL" + extension);
    }
}