using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maniac.Core.Settings;

public sealed class SettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string FilePath { get; }
    public ManiacSettings Current { get; private set; }

    public event Action<ManiacSettings>? Changed;

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
        Current = LoadOrDefault(FilePath);
    }

    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "maniac-lazer");

    public static string DefaultPath() => Path.Combine(DefaultDirectory(), "settings.json");

    public void Update(Action<ManiacSettings> mutate)
    {
        mutate(Current);
        Save();
        Changed?.Invoke(Current);
    }

    public void Replace(ManiacSettings settings)
    {
        Current = Sanitize(settings);
        Save();
        Changed?.Invoke(Current);
    }

    public void Reset() => Replace(new ManiacSettings());

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        Current.Version = ManiacSettings.CurrentVersion;
        string json = JsonSerializer.Serialize(Current, JsonOptions);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(FilePath))
            File.Replace(temp, FilePath, destinationBackupFileName: null);
        else
            File.Move(temp, FilePath);
    }

    public static ManiacSettings LoadOrDefault(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new ManiacSettings();
            string json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<ManiacSettings>(json, JsonOptions);
            return Sanitize(loaded ?? new ManiacSettings());
        }
        catch
        {
            return new ManiacSettings();
        }
    }

    static ManiacSettings Sanitize(ManiacSettings settings)
    {
        settings.General ??= new GeneralSettings();
        settings.Osu ??= new OsuSettings();
        settings.Mania ??= new ManiaSettings();
        if (string.IsNullOrWhiteSpace(settings.PanelHotkey))
            settings.PanelHotkey = "F7";
        if (string.IsNullOrWhiteSpace(settings.AutoplayHotkey))
            settings.AutoplayHotkey = "F8";
        if (settings.Version < 1)
            settings.Version = ManiacSettings.CurrentVersion;
        return settings;
    }
}
