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

        if (File.Exists(SettingsPath))
        {
            var backupPath = $"{SettingsPath}.bak";
            File.Copy(SettingsPath, backupPath, true);
        }

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions);
    }

    public async Task<MixerConfiguration> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            return new MixerConfiguration();
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<MixerConfiguration>(stream, JsonOptions) ?? new MixerConfiguration();
        }
        catch
        {
            var backupPath = $"{SettingsPath}.bak";
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, SettingsPath, true);
                await using var backupStream = File.OpenRead(SettingsPath);
                return await JsonSerializer.DeserializeAsync<MixerConfiguration>(backupStream, JsonOptions) ?? new MixerConfiguration();
            }

            return new MixerConfiguration();
        }
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

    public Task DeletePresetAsync(string name)
    {
        var path = Path.Combine(PresetsFolder, $"{SanitizeFileName(name)}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> RenamePresetAsync(string sourceName, string targetName)
    {
        var sourcePath = Path.Combine(PresetsFolder, $"{SanitizeFileName(sourceName)}.json");
        var targetPath = Path.Combine(PresetsFolder, $"{SanitizeFileName(targetName)}.json");

        if (!File.Exists(sourcePath) || File.Exists(targetPath))
        {
            return false;
        }

        await using (var stream = File.OpenRead(sourcePath))
        {
            var preset = await JsonSerializer.DeserializeAsync<MixerPreset>(stream, JsonOptions);
            if (preset is null)
            {
                return false;
            }

            preset.Name = targetName;
            await SavePresetAsync(preset);
        }

        File.Delete(sourcePath);
        return true;
    }

    public async Task<bool> DuplicatePresetAsync(string sourceName, string targetName)
    {
        var source = await LoadPresetAsync(sourceName);
        if (source is null)
        {
            return false;
        }

        var targetPath = Path.Combine(PresetsFolder, $"{SanitizeFileName(targetName)}.json");
        if (File.Exists(targetPath))
        {
            return false;
        }

        source.Name = targetName;
        await SavePresetAsync(source);
        return true;
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
