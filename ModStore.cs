using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EaWModLauncher;

/// <summary>Lädt und speichert die Mod-Liste und die zwischengespeicherten Vorschaubilder.</summary>
public static class ModStore
{
    public static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EaWModLauncher");

    public static readonly string ImageDir = Path.Combine(BaseDir, "images");

    private static readonly string DataFile = Path.Combine(BaseDir, "mods.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static LauncherData Load()
    {
        try
        {
            if (File.Exists(DataFile))
                return JsonSerializer.Deserialize<LauncherData>(File.ReadAllText(DataFile), JsonOptions) ?? new();
        }
        catch (Exception)
        {
            // Beschädigte Datei sichern, statt sie beim nächsten Speichern zu überschreiben.
            try { File.Copy(DataFile, DataFile + ".defekt", overwrite: true); } catch { }
        }
        return new LauncherData();
    }

    public static void Save(LauncherData data)
    {
        Directory.CreateDirectory(BaseDir);
        var tmp = DataFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(tmp, DataFile, overwrite: true);
    }

    /// <summary>Speichert Bilddaten für eine Mod und gibt den Dateinamen zurück.</summary>
    public static string SaveImage(string modId, byte[] bytes)
    {
        Directory.CreateDirectory(ImageDir);
        DeleteImage(modId);
        var file = modId + GuessExtension(bytes);
        File.WriteAllBytes(Path.Combine(ImageDir, file), bytes);
        return file;
    }

    public static void DeleteImage(string modId)
    {
        if (!Directory.Exists(ImageDir)) return;
        foreach (var f in Directory.GetFiles(ImageDir, modId + ".*"))
        {
            try { File.Delete(f); } catch { }
        }
    }

    public static ImageSource? LoadImage(string? file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        var path = Path.Combine(ImageDir, file);
        if (!File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;          // Datei nicht sperren
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.DecodePixelWidth = 440;                          // große Workshop-Bilder verkleinern
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static string GuessExtension(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b.Length > 2 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ".gif";
        return ".jpg";
    }
}
