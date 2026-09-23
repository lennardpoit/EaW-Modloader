using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace EaWModLauncher;

public record UpdateInfo(Version Version, string DownloadUrl, long Size, string PageUrl);

/// <summary>
/// Sucht auf GitHub nach einer neueren Version und installiert sie auf Wunsch.
/// Eine Version wird als Release mit dem Tag "vX.Y.Z" und der Datei "EaWModLauncher.exe" veröffentlicht.
/// </summary>
public static class UpdateService
{
    public const string Repository = "lennardpoit/EaW-Modloader";
    private const string AssetName = "EaWModLauncher.exe";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"EaWModLauncher/{CurrentVersion.ToString(3)}"); // von GitHub verlangt
        return client;
    }

    /// <summary>Version dieses Programms (aus &lt;Version&gt; in der .csproj).</summary>
    public static Version CurrentVersion => Normalize(Assembly.GetExecutingAssembly().GetName().Version);

    private static Version Normalize(Version? v) =>
        v == null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, Math.Max(0, v.Build));

    /// <summary>Neueste Version auf GitHub, falls sie neuer ist als diese. Bei Fehlern (offline usw.) null.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repository}/releases/latest");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var resp = await Http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return null; // z. B. noch kein Release veröffentlicht

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var latest)) return null;
            latest = Normalize(latest);
            if (latest <= CurrentVersion) return null;

            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            page ??= $"https://github.com/{Repository}/releases/latest";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (!string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                    continue;
                return new UpdateInfo(latest,
                    asset.GetProperty("browser_download_url").GetString()!,
                    asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    page);
            }
            return new UpdateInfo(latest, "", 0, page); // Release ohne .exe: nur Hinweis mit Link
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lädt die neue .exe herunter, ersetzt die laufende Datei und startet die neue Version.
    /// Eine laufende .exe darf unter Windows umbenannt (aber nicht überschrieben) werden: die alte wird zu
    /// "EaWModLauncher.exe.old" und beim nächsten Start gelöscht.
    /// </summary>
    public static async Task InstallAsync(UpdateInfo update, IProgress<double> progress)
    {
        if (string.IsNullOrEmpty(update.DownloadUrl))
            throw new InvalidOperationException("Dieses Release enthält keine EaWModLauncher.exe.");

        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Programmpfad unbekannt.");
        var tmp = exe + ".download";
        var old = exe + ".old";

        // 1. Herunterladen (in denselben Ordner, damit das spätere Verschieben sofort geht)
        using (var resp = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? update.Size;
            await using var input = await resp.Content.ReadAsStreamAsync();
            await using var output = File.Create(tmp);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        // 2. Prüfen: vollständig und eine Windows-Programmdatei
        var info = new FileInfo(tmp);
        bool complete = update.Size <= 0 || info.Length == update.Size;
        bool isExe;
        using (var fs = File.OpenRead(tmp))
            isExe = fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
        if (!complete || !isExe)
        {
            File.Delete(tmp);
            throw new InvalidOperationException("Der Download ist unvollständig oder beschädigt.");
        }

        // 3. Austauschen – bei Fehlern den alten Zustand wiederherstellen
        if (File.Exists(old)) File.Delete(old);
        File.Move(exe, old);
        try
        {
            File.Move(tmp, exe);
        }
        catch
        {
            File.Move(old, exe);
            throw;
        }

        // 4. Neue Version starten
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
    }

    /// <summary>Löscht die alte .exe nach einem Update (wartet ggf., bis der alte Prozess beendet ist).</summary>
    public static async Task CleanupAsync()
    {
        var exe = Environment.ProcessPath;
        if (exe == null) return;
        foreach (var leftover in new[] { exe + ".old", exe + ".download" })
        {
            for (int attempt = 0; attempt < 20 && File.Exists(leftover); attempt++)
            {
                try { File.Delete(leftover); }
                catch { await Task.Delay(500); }
            }
        }
    }

    public static void OpenPage(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
