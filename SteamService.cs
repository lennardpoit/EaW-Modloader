using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EaWModLauncher;

public record WorkshopDetails(string Id, string Title, string? PreviewUrl, long ConsumerAppId);

/// <summary>Zugriff auf die Steam-Web-API und den lokalen Steam-Client.</summary>
public static class SteamService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Liest Titel und Vorschaubild-URL eines Workshop-Items (öffentliche API, kein Key nötig).</summary>
    public static async Task<WorkshopDetails> GetDetailsAsync(string id)
    {
        var list = await GetDetailsAsync(new[] { id });
        return list.Count > 0
            ? list[0]
            : throw new InvalidOperationException(
                Loc.T("ErrItemNotFound", id));
    }

    /// <summary>Fragt mehrere Items in einem Aufruf ab. Nicht gefundene Items fehlen im Ergebnis.</summary>
    public static async Task<List<WorkshopDetails>> GetDetailsAsync(IReadOnlyList<string> ids)
    {
        var fields = new Dictionary<string, string> { ["itemcount"] = ids.Count.ToString() };
        for (int i = 0; i < ids.Count; i++)
            fields[$"publishedfileids[{i}]"] = ids[i];

        using var resp = await Http.PostAsync(
            "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
            new FormUrlEncodedContent(fields));
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = new List<WorkshopDetails>();
        foreach (var item in doc.RootElement.GetProperty("response").GetProperty("publishedfiledetails").EnumerateArray())
        {
            if (!item.TryGetProperty("result", out var r) || r.GetInt32() != 1) continue;

            string id = item.GetProperty("publishedfileid").GetString() ?? "";
            string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            string? preview = item.TryGetProperty("preview_url", out var p) ? p.GetString() : null;
            long appId = item.TryGetProperty("consumer_app_id", out var a) ? a.GetInt64() : 0;

            result.Add(new WorkshopDetails(id, string.IsNullOrWhiteSpace(title) ? $"Mod {id}" : title,
                string.IsNullOrWhiteSpace(preview) ? null : preview, appId));
        }
        return result;
    }

    public static Task<byte[]> DownloadAsync(string url) => Http.GetByteArrayAsync(url);

    /// <summary>Akzeptiert eine reine ID oder einen Workshop-Link (…?id=123456).</summary>
    public static string? ParseModId(string input)
    {
        input = input.Trim();
        if (Regex.IsMatch(input, @"^\d{3,20}$")) return input;
        var m = Regex.Match(input, @"[?&]id=(\d{3,20})");
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string? FindSteamExe()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamExe") is string exe && File.Exists(exe))
                return Path.GetFullPath(exe);
        }
        catch { }

        var dir = FindSteamDir();
        if (dir != null)
        {
            var exe = Path.Combine(dir, "steam.exe");
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    public static string? FindSteamDir()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (key?.GetValue("SteamPath") is string p && Directory.Exists(p)) return Path.GetFullPath(p);
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                if (key?.GetValue("InstallPath") is string p && Directory.Exists(p)) return p;
        }
        catch { }
        return null;
    }

    /// <summary>Alle Steam-Bibliotheksordner (aus libraryfolders.vdf).</summary>
    private static List<string> GetLibraryFolders()
    {
        var result = new List<string>();
        var steamDir = FindSteamDir();
        if (steamDir == null) return result;
        result.Add(steamDir);

        var vdf = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return result;
        try
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                var path = m.Groups[1].Value.Replace(@"\\", @"\");
                if (!result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// true = Mod-Ordner gefunden, false = nicht gefunden, null = konnte nicht geprüft werden.
    /// </summary>
    public static bool? IsModDownloaded(int appId, string modId)
    {
        var dirs = GetWorkshopContentDirs(appId);
        return dirs == null ? null : dirs.Any(d => Directory.Exists(Path.Combine(d, modId)));
    }

    /// <summary>
    /// IDs aller abonnierten (heruntergeladenen) Workshop-Mods – ein Unterordner pro Mod im Workshop-Verzeichnis.
    /// null, wenn keine Steam-Bibliothek gefunden wurde.
    /// </summary>
    public static List<string>? GetSubscribedModIds(int appId)
    {
        var dirs = GetWorkshopContentDirs(appId);
        if (dirs == null) return null;
        return dirs
            .SelectMany(d => { try { return Directory.GetDirectories(d); } catch { return Array.Empty<string>(); } })
            .Select(Path.GetFileName)
            .Where(name => name != null && Regex.IsMatch(name, @"^\d+$"))
            .Select(name => name!)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Alle vorhandenen Workshop-Ordner des Spiels (…\steamapps\workshop\content\&lt;appId&gt;).
    /// null, wenn keine Steam-Bibliothek gefunden wurde (dann ist keine Prüfung möglich).
    /// </summary>
    public static List<string>? GetWorkshopContentDirs(int appId)
    {
        var libs = GetLibraryFolders();
        if (libs.Count == 0) return null;
        return libs
            .Select(lib => Path.Combine(lib, "steamapps", "workshop", "content", appId.ToString()))
            .Where(Directory.Exists)
            .ToList();
    }

    /// <summary>Installationsordner des Spiels (aus appmanifest_&lt;appId&gt;.acf).</summary>
    public static string? FindGameDir(int appId)
    {
        foreach (var lib in GetLibraryFolders())
        {
            var manifest = Path.Combine(lib, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest)) continue;
            try
            {
                var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                if (!m.Success) continue;
                var dir = Path.Combine(lib, "steamapps", "common", m.Groups[1].Value);
                if (Directory.Exists(dir)) return dir;
            }
            catch { }
        }
        return null;
    }

    /// <summary>true, wenn Steam läuft und ein Nutzer angemeldet ist.</summary>
    private static bool IsSteamReady()
    {
        if (Process.GetProcessesByName("steam").Length == 0) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            return key?.GetValue("ActiveUser") is int user && user != 0;
        }
        catch
        {
            return true; // Nicht prüfbar – Steam läuft jedenfalls.
        }
    }

    /// <summary>Startet Steam im Hintergrund, falls nötig, und wartet auf die Anmeldung.</summary>
    private static async Task EnsureSteamRunningAsync(IProgress<string>? progress)
    {
        if (IsSteamReady()) return;

        var steamExe = FindSteamExe()
            ?? throw new InvalidOperationException(Loc.T("ErrSteamNotFound"));
        progress?.Report(Loc.T("SteamStarting"));
        if (Process.GetProcessesByName("steam").Length == 0)
            Process.Start(new ProcessStartInfo(steamExe, "-silent") { UseShellExecute = false });

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!IsSteamReady())
        {
            if (DateTime.UtcNow > deadline)
                throw new InvalidOperationException(
                    Loc.T("ErrSteamNotReady"));
            await Task.Delay(500);
        }
        await Task.Delay(3000); // Steam kurz Zeit geben, die Workshop-Dienste hochzufahren.
    }

    /// <summary>
    /// Startet Forces of Corruption direkt (corruption\StarWarsG.exe) mit den angegebenen Startoptionen.
    /// Steams eigene Startoption für App 32470 würde das Hauptspiel Empire at War starten.
    /// Die steam_appid.txt im corruption-Ordner sorgt dafür, dass sich das Spiel selbst bei Steam anmeldet
    /// und STEAMMOD=… die Workshop-Mod lädt.
    /// </summary>
    public static async Task LaunchForcesOfCorruptionAsync(int appId, string launchOptions, IProgress<string>? progress = null)
    {
        var gameDir = FindGameDir(appId)
            ?? throw new InvalidOperationException(Loc.T("ErrGameNotFound"));
        var focDir = Path.Combine(gameDir, "corruption");
        var exe = Path.Combine(focDir, "StarWarsG.exe");
        if (!File.Exists(exe))
            throw new InvalidOperationException(Loc.T("ErrFocNotFound", exe));

        await EnsureSteamRunningAsync(progress);

        Process.Start(new ProcessStartInfo(exe, launchOptions)
        {
            WorkingDirectory = focDir,
            UseShellExecute = false,
        });
    }

    public static void OpenWorkshopPage(string modId)
    {
        try
        {
            Process.Start(new ProcessStartInfo($"steam://url/CommunityFilePage/{modId}") { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo(
                $"https://steamcommunity.com/sharedfiles/filedetails/?id={modId}") { UseShellExecute = true });
        }
    }
}
