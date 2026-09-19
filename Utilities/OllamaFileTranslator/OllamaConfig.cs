using System.Collections.Generic;

namespace OllamaFileTranslator;

public class OllamaConfig
{
    public OllamaConfig()
    {
        ModelParams = new Dictionary<string, object>();
    }

    public string Url { get; set; }
    public string Model { get; set; }
    public Dictionary<string, object> ModelParams { get; set; }
    public string SystemPrompt { get; set; }
    public string GlossaryPrompt { get; set; }
}