# OllamaFileTranslator

A utility for translating text files using a local Ollama server via the chat API.

Partially based on https://github.com/joshfreitas1984/XUnity.AutoTranslate.LlmTranslators

## Features

- Translates text files line-by-line
- Supports folder scanning (recursively) and individual file translation
- Saves output files with `_TL` suffix in the same directory as the original
- Uses the Ollama chat API with configurable models and parameters
- Escapes special characters (`=` and `\`) in output format
- Output format: `original=translation`

## Requirements

- .NET 6 or later
- A running Ollama server (default: `http://localhost:11434`)
- A compatible translation model installed in Ollama

## Setup

1. Place the `ollama-config.yaml` file in the same directory as the executable
2. Ensure your Ollama server is running

## Usage

```
OllamaFileTranslator.exe <folder-or-file> [folder-or-file] ...
```

### Examples

Translate all `.txt` files in a directory:
```
OllamaFileTranslator.exe E:\dump
```

Translate a single file:
```
OllamaFileTranslator.exe E:\file.txt
```

Translate multiple paths:
```
OllamaFileTranslator.exe E:\file1.txt E:\folder1 E:\folder2
```

## Configuration

Create a YAML configuration file with the following structure:

```yaml
apiKey: "None"
apiKeyRequired: false
url: "http://localhost:11434/api/chat"
model: "huihui_ai/hy-mt1.5-abliterated"
modelParams:
  temperature: 0.2
  max_tokens: 4096
  top_p: 0.9
  top_k: 40
  min_p: 0.5
  frequency_penalty: 0
  presence_penalty: 0
  num_ctx: 8192
systemPrompt: |
  Your translation prompt here...
glossaryPrompt: |
  Glossary and additional context here...
```

### Configuration Options

- **apiKey**: API key (if required by your Ollama setup)
- **apiKeyRequired**: Whether an API key is required
- **url**: Ollama API endpoint (chat endpoint)
- **model**: Model name to use for translation
- **modelParams**: Model parameters (temperature, max_tokens, etc.)
- **systemPrompt**: System prompt defining translation rules and style
- **glossaryPrompt**: Additional glossary and context for consistent translations

## Output Format

The translated files use the format: `original_text=translated_text`

Special characters are escaped:
- `\` becomes `\\`
- `=` becomes `\=`

## Output Files

Original files are not modified. Translated files are saved with the `_TL` suffix:
- `example.txt` ? `example_TL.txt`
- Files maintain the same directory structure as the original

## Error Handling

- Failed translations are marked with `[Translation failed]` in the output
- Progress is reported every 10 lines
- Detailed error messages are written to the console
