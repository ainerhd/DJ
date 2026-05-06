using System.IO;
using System.Text.Json;

namespace AudioMixerController.App;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioMixerController");

    public string SettingsPath => Path.Combine(_baseFolder, "settings.json");
    public string PresetsFolder => Path.Combine(_baseFolder, "presets");

    public async Task SaveAsync(MixerConfiguration configuration)
    {
        Directory.CreateDirectory(_baseFolder);
        Directory.CreateDirectory(PresetsFolder);

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions);
    }

    public async Task<MixerConfiguration> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            return new MixerConfiguration();
        }

        await using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync<MixerConfiguration>(stream, JsonOptions) ?? new MixerConfiguration();
    }

    public async Task SavePresetAsync(MixerPreset preset)
    {
        Directory.CreateDirectory(PresetsFolder);
        var path = Path.Combine(PresetsFolder, $"{SanitizeFileName(preset.Name)}.json");

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, preset, JsonOptions);
    }

    public async Task<MixerPreset?> LoadPresetAsync(string name)
    {
        var path = Path.Combine(PresetsFolder, $"{SanitizeFileName(name)}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MixerPreset>(stream, JsonOptions);
    }

    public IReadOnlyList<string> GetPresetNames()
    {
        if (!Directory.Exists(PresetsFolder))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(PresetsFolder, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();
    }
}
