using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EaWModLauncher;

public partial class MainWindow : Window
{
    private const string ReadyText = "Bereit. Klick auf eine Kachel startet die Mod, Rechtsklick für weitere Optionen.";

    private readonly LauncherData _data;
    private readonly ObservableCollection<ModEntry> _mods;
    private readonly ObservableCollection<ModEntry> _suggestions = new();

    /// <summary>Alle vorgeschlagenen Mods (auch die gerade nicht angezeigten), nach ID.</summary>
    private readonly Dictionary<string, ModEntry> _suggestionEntries = new();

    public MainWindow()
    {
        InitializeComponent();

        _data = ModStore.Load();
        _mods = new ObservableCollection<ModEntry>(_data.Mods);
        foreach (var mod in _mods)
            mod.Preview = ModStore.LoadImage(mod.ImageFile);

        var cache = _data.SuggestionCache.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var (id, fallbackTitle) in Suggestions.Popular)
        {
            cache.TryGetValue(id, out var cached);
            var own = _mods.FirstOrDefault(m => m.Id == id);
            var entry = new ModEntry
            {
                Id = id,
                Title = cached?.Title ?? own?.Title ?? fallbackTitle,
                ImageFile = cached?.ImageFile ?? own?.ImageFile,
                IsSuggestion = true,
            };
            entry.Preview = own?.Preview ?? ModStore.LoadImage(entry.ImageFile);
            _suggestionEntries[id] = entry;
        }

        SyncSubscriptions();
        RefreshDownloadState();
        RebuildSuggestions();

        ModList.ItemsSource = _mods;
        SuggestionList.ItemsSource = _suggestions;
        _mods.CollectionChanged += (_, _) => UpdateCounts();
        UpdateCounts();

        ContentArea.Background = CreateStarfield();
        Icon = GameAssets.LoadIcon(_data.AppId);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Logo und Emblem kommen aus der lokalen Spielinstallation (nicht im Launcher enthalten).
        var (logo, emblem) = await Task.Run(() => GameAssets.LoadArt(_data.AppId));
        LogoImage.Source = logo;
        LogoFallback.Visibility = logo == null ? Visibility.Visible : Visibility.Collapsed;
        Resources["EmblemImage"] = emblem;

        _ = UpdateService.CleanupAsync();
        _ = CheckForUpdateAsync();
        await LoadMissingDetailsAsync();
    }

    // ---------- Updates ----------

    private UpdateInfo? _update;

    private async Task CheckForUpdateAsync()
    {
        _update = await UpdateService.CheckAsync();
        if (_update == null) return;

        UpdateText.Text = $"Neue Version {_update.Version.ToString(3)} verfügbar " +
                          $"(installiert: {UpdateService.CurrentVersion.ToString(3)}).";
        UpdateButton.Visibility = _update.DownloadUrl.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_update == null) return;
        UpdateButton.IsEnabled = false;
        UpdateDismissButton.IsEnabled = false;
        var version = _update.Version.ToString(3);
        try
        {
            await UpdateService.InstallAsync(_update,
                new Progress<double>(p => UpdateText.Text = $"Lade Version {version} herunter … {p:P0}"));
            UpdateText.Text = $"Version {version} installiert – Neustart …";
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateText.Text = $"Neue Version {version} verfügbar.";
            UpdateButton.IsEnabled = true;
            UpdateDismissButton.IsEnabled = true;
            if (Ask($"Das automatische Update ist fehlgeschlagen:\n{ex.Message}\n\n" +
                    "Download-Seite im Browser öffnen, um die neue Version von Hand herunterzuladen?"))
                UpdateService.OpenPage(_update.PageUrl);
        }
    }

    private void UpdateDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_update != null) UpdateService.OpenPage(_update.PageUrl);
    }

    private void UpdateDismiss_Click(object sender, RoutedEventArgs e) =>
        UpdateBanner.Visibility = Visibility.Collapsed;

    /// <summary>Nach dem Wechsel zurück ins Fenster prüfen, ob inzwischen Mods abonniert wurden.</summary>
    private async void Window_Activated(object? sender, EventArgs e)
    {
        int added = SyncSubscriptions();
        RefreshDownloadState();
        if (added > 0)
        {
            SetStatus(added == 1
                ? "1 neu abonnierte Mod wurde zu „Meine Mods“ hinzugefügt."
                : $"{added} neu abonnierte Mods wurden zu „Meine Mods“ hinzugefügt.");
            await LoadMissingDetailsAsync();
        }
    }

    private IEnumerable<ModEntry> AllEntries => _mods.Concat(_suggestionEntries.Values);

    /// <summary>
    /// Nimmt alle abonnierten Workshop-Mods in „Meine Mods“ auf, die dort noch fehlen
    /// (außer denen, die der Nutzer bewusst entfernt hat). Gibt die Anzahl neu aufgenommener Mods zurück.
    /// </summary>
    private int SyncSubscriptions()
    {
        var subscribed = SteamService.GetSubscribedModIds(_data.AppId);
        if (subscribed == null) return 0;

        var own = _mods.Select(m => m.Id).ToHashSet();
        var popularity = Suggestions.Popular.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var newIds = subscribed
            .Where(id => !own.Contains(id) && !_data.RemovedSubscriptions.Contains(id))
            .OrderBy(id => popularity.TryGetValue(id, out var rank) ? rank : int.MaxValue)
            .ThenBy(id => long.Parse(id))
            .ToList();
        if (newIds.Count == 0) return 0;

        foreach (var id in newIds)
        {
            _suggestionEntries.TryGetValue(id, out var known);
            _mods.Add(new ModEntry
            {
                Id = id,
                Title = known?.Title ?? $"Mod {id}",
                ImageFile = known?.ImageFile,
                Preview = known?.Preview,
            });
        }
        Persist();
        RebuildSuggestions();
        return newIds.Count;
    }

    private void RefreshDownloadState()
    {
        var dirs = SteamService.GetWorkshopContentDirs(_data.AppId);
        foreach (var entry in AllEntries)
            entry.IsDownloaded = dirs == null || dirs.Any(d => Directory.Exists(Path.Combine(d, entry.Id)));
    }

    /// <summary>Stellt die angezeigten Vorschläge neu zusammen: abonnierte zuerst, sonst nach Beliebtheit.</summary>
    private void RebuildSuggestions()
    {
        var own = _mods.Select(m => m.Id).ToHashSet();
        var visible = Suggestions.Popular
            .Select(p => _suggestionEntries[p.Id])
            .Where(s => !own.Contains(s.Id) && !_data.HiddenSuggestions.Contains(s.Id))
            .OrderBy(s => s.IsDownloaded ? 0 : 1)
            .ToList();

        _suggestions.Clear();
        foreach (var s in visible) _suggestions.Add(s);

        int hidden = _data.HiddenSuggestions.Count(Suggestions.Contains);
        ShowHiddenButton.Content = $"Ausgeblendete anzeigen ({hidden})";
        ShowHiddenButton.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
        SuggestionHeader.Visibility = visible.Count > 0 || hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        OwnEmptyHint.Visibility = _mods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"{_mods.Count} eigene · {_suggestions.Count} vorgeschlagen · v{UpdateService.CurrentVersion.ToString(3)}";
    }

    private bool _loadingDetails, _detailsRequested;
    private readonly HashSet<string> _detailsLoaded = new();

    /// <summary>
    /// Lädt Namen und Bilder aller Kacheln (eigene Mods und Vorschläge), die noch kein Bild haben –
    /// gebündelt in einem Workshop-Aufruf. Pro Sitzung wird jede Mod höchstens einmal erfolgreich abgefragt.
    /// </summary>
    private async Task LoadMissingDetailsAsync()
    {
        _detailsRequested = true;
        if (_loadingDetails) return; // läuft schon – wird am Ende erneut geprüft
        _loadingDetails = true;
        try
        {
            while (_detailsRequested)
            {
                _detailsRequested = false;
                var ids = AllEntries
                    .Where(e => !e.HasPreview && !_detailsLoaded.Contains(e.Id))
                    .Select(e => e.Id)
                    .Distinct()
                    .ToList();
                if (ids.Count == 0) break;

                SetStatus("Lade Namen und Bilder aus dem Steam Workshop …");
                var details = await SteamService.GetDetailsAsync(ids);
                foreach (var d in details)
                {
                    var entries = AllEntries.Where(e => e.Id == d.Id).ToList();
                    foreach (var entry in entries) entry.Title = d.Title;
                    if (entries.Count > 0) await TryDownloadImage(entries[0], d.PreviewUrl);
                }
                _detailsLoaded.UnionWith(ids);
                Persist();
                SetStatus(ReadyText);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            SetStatus("Namen und Bilder konnten nicht vom Steam Workshop geladen werden (offline?). Beim nächsten Start wird es erneut versucht.");
        }
        finally
        {
            _loadingDetails = false;
        }
    }

    /// <summary>Kachelbarer Sternenhimmel als Hintergrund.</summary>
    private static ImageBrush CreateStarfield()
    {
        const int size = 512;
        var rnd = new Random(1977);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            for (int i = 0; i < 150; i++)
            {
                var pos = new Point(3 + rnd.NextDouble() * (size - 6), 3 + rnd.NextDouble() * (size - 6));
                double r = rnd.NextDouble() < 0.9 ? 0.5 + rnd.NextDouble() * 0.5 : 1.1 + rnd.NextDouble() * 0.6;
                byte a = (byte)(35 + rnd.Next(150));
                var color = rnd.NextDouble() < 0.15 ? Color.FromArgb(a, 0xC8, 0xD4, 0xFF) : Color.FromArgb(a, 0xFF, 0xFF, 0xFF);
                dc.DrawEllipse(new SolidColorBrush(color), null, pos, r, r);
            }
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = 0.6,
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Dunkle Titelleiste (Windows 10/11) passend zum Farbschema.</summary>
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));        // DWMWA_USE_IMMERSIVE_DARK_MODE
        int caption = 0x00000000;                                      // COLORREF (0x00BBGGRR): Schwarz
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));     // DWMWA_CAPTION_COLOR (Windows 11)
        int border = 0x00342B27;                                       // #272B34
        DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));      // DWMWA_BORDER_COLOR (Windows 11)
    }

    private void Persist()
    {
        _data.Mods = _mods.ToList();
        _data.SuggestionCache = _suggestionEntries.Values.ToList();
        try
        {
            ModStore.Save(_data);
        }
        catch (Exception ex)
        {
            ShowError("Die Mod-Liste konnte nicht gespeichert werden:\n" + ex.Message);
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void ShowError(string text) =>
        MessageBox.Show(this, text, "EaW Mod-Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);

    private bool Ask(string text) =>
        MessageBox.Show(this, text, "EaW Mod-Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    // ---------- Hinzufügen ----------

    private void ModInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        InputHint.Visibility = ModInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ModInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddButton_Click(sender, e);
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AddButton.IsEnabled) return;

        var id = SteamService.ParseModId(ModInput.Text);
        if (id == null)
        {
            ShowError("Bitte eine gültige Workshop-ID (nur Ziffern) oder einen Workshop-Link eingeben.\n\n" +
                      "Beispiel: 1125571106 oder https://steamcommunity.com/sharedfiles/filedetails/?id=1125571106");
            return;
        }
        if (_mods.Any(m => m.Id == id))
        {
            ShowError($"Die Mod {id} ist bereits in der Liste.");
            return;
        }

        // Vorgeschlagene Mod: Name und Bild sind schon da.
        if (_suggestionEntries.TryGetValue(id, out var suggestion) && suggestion.HasPreview)
        {
            Adopt(suggestion);
            ModInput.Clear();
            return;
        }

        AddButton.IsEnabled = false;
        SetStatus($"Lade Informationen zu Mod {id} vom Steam Workshop …");
        try
        {
            var entry = new ModEntry { Id = id, Title = $"Mod {id}" };
            try
            {
                var details = await SteamService.GetDetailsAsync(id);
                if (details.ConsumerAppId != 0 && details.ConsumerAppId != _data.AppId &&
                    !Ask($"„{details.Title}“ gehört laut Steam nicht zu Empire at War (App {details.ConsumerAppId}).\n\nTrotzdem hinzufügen?"))
                {
                    SetStatus("Hinzufügen abgebrochen.");
                    return;
                }
                entry.Title = details.Title;
                await TryDownloadImage(entry, details.PreviewUrl);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (!Ask($"Der Steam Workshop ist gerade nicht erreichbar:\n{ex.Message}\n\n" +
                         "Mod trotzdem ohne Namen und Bild hinzufügen? (Später per Rechtsklick → „aktualisieren“ nachladen.)"))
                {
                    SetStatus("Hinzufügen abgebrochen.");
                    return;
                }
            }

            entry.IsDownloaded = SteamService.IsModDownloaded(_data.AppId, id) != false;
            _data.RemovedSubscriptions.Remove(id);
            _mods.Add(entry);
            Persist();
            RebuildSuggestions();
            ModInput.Clear();

            var status = $"„{entry.Title}“ hinzugefügt.";
            if (!entry.IsDownloaded)
                status += " Hinweis: Die Mod ist noch nicht heruntergeladen – bitte im Workshop abonnieren.";
            SetStatus(status);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SetStatus("Hinzufügen fehlgeschlagen.");
        }
        finally
        {
            AddButton.IsEnabled = true;
        }
    }

    /// <summary>Übernimmt einen Vorschlag in „Meine Mods“.</summary>
    private void Adopt(ModEntry suggestion)
    {
        _data.RemovedSubscriptions.Remove(suggestion.Id);
        _mods.Add(new ModEntry
        {
            Id = suggestion.Id,
            Title = suggestion.Title,
            ImageFile = suggestion.ImageFile,
            Preview = suggestion.Preview,
            IsDownloaded = suggestion.IsDownloaded,
        });
        Persist();
        RebuildSuggestions();
        SetStatus($"„{suggestion.Title}“ zu „Meine Mods“ hinzugefügt.");
    }

    /// <summary>Lädt das Vorschaubild herunter und speichert es lokal. Fehler sind nicht fatal.</summary>
    private async Task TryDownloadImage(ModEntry entry, string? url)
    {
        if (url == null) return;
        try
        {
            var bytes = await SteamService.DownloadAsync(url);
            entry.ImageFile = ModStore.SaveImage(entry.Id, bytes);
            entry.Preview = ModStore.LoadImage(entry.ImageFile);

            // Eigene Mod und Vorschlag mit derselben ID teilen sich die Bilddatei.
            foreach (var other in AllEntries.Where(o => o != entry && o.Id == entry.Id))
            {
                other.ImageFile = entry.ImageFile;
                other.Preview = entry.Preview;
            }
        }
        catch
        {
            // Ohne Bild weitermachen; Platzhalter wird angezeigt.
        }
    }

    // ---------- Starten ----------

    private bool _launching;

    private static ModEntry? EntryOf(object sender) => (sender as FrameworkElement)?.DataContext as ModEntry;

    /// <summary>Klick auf eine Kachel: abonnierte Mods starten, sonst Workshop-Seite zum Abonnieren öffnen.</summary>
    private async void Tile_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } mod) return;

        if (!mod.IsDownloaded)
        {
            SteamService.OpenWorkshopPage(mod.Id);
            SetStatus($"Workshop-Seite von „{mod.Title}“ geöffnet. Nach dem Abonnieren und Herunterladen startet ein Klick die Mod.");
            return;
        }

        await StartModAsync(mod);
    }

    /// <summary>Menü „Starten“: startet auch dann, wenn die Mod nicht als heruntergeladen erkannt wurde.</summary>
    private async void MenuStart_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } mod) await StartModAsync(mod);
    }

    private Task StartModAsync(ModEntry mod) =>
        StartGameAsync($"STEAMMOD={mod.Id}",
            $"Starte Forces of Corruption mit „{mod.Title}“ (STEAMMOD={mod.Id}) …");

    private async void Vanilla_Click(object sender, RoutedEventArgs e) =>
        await StartGameAsync("", "Starte Forces of Corruption ohne Mod …");

    private async Task StartGameAsync(string launchOptions, string status)
    {
        if (_launching) return; // Doppelklick verhindert doppelten Start
        _launching = true;
        try
        {
            await SteamService.LaunchForcesOfCorruptionAsync(_data.AppId, launchOptions,
                new Progress<string>(SetStatus));
            SetStatus(status);
        }
        catch (Exception ex)
        {
            ShowError("Forces of Corruption konnte nicht gestartet werden:\n" + ex.Message);
            SetStatus("Start fehlgeschlagen.");
        }
        finally
        {
            // Kurze Sperre, damit schnelle Mehrfachklicks nicht mehrere Instanzen öffnen.
            await Task.Delay(3000);
            _launching = false;
        }
    }

    // ---------- Kontextmenü ----------

    private void MenuAdopt_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { IsSuggestion: true } mod) Adopt(mod);
    }

    private void MenuHide_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { IsSuggestion: true } mod) return;
        if (!_data.HiddenSuggestions.Contains(mod.Id)) _data.HiddenSuggestions.Add(mod.Id);
        Persist();
        RebuildSuggestions();
        SetStatus($"Vorschlag „{mod.Title}“ ausgeblendet.");
    }

    private void ShowHidden_Click(object sender, RoutedEventArgs e)
    {
        _data.HiddenSuggestions.Clear();
        Persist();
        RebuildSuggestions();
        SetStatus("Alle Vorschläge werden wieder angezeigt.");
    }

    private async void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } mod) return;
        SetStatus($"Aktualisiere „{mod.Title}“ …");
        try
        {
            var details = await SteamService.GetDetailsAsync(mod.Id);
            mod.Title = details.Title;
            await TryDownloadImage(mod, details.PreviewUrl);
            Persist();
            SetStatus($"„{mod.Title}“ aktualisiert.");
        }
        catch (Exception ex)
        {
            ShowError("Aktualisieren fehlgeschlagen:\n" + ex.Message);
            SetStatus("Aktualisieren fehlgeschlagen.");
        }
    }

    private void MenuWorkshop_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } mod) SteamService.OpenWorkshopPage(mod.Id);
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } mod) return;
        Clipboard.SetText($"STEAMMOD={mod.Id}");
        SetStatus($"„STEAMMOD={mod.Id}“ in die Zwischenablage kopiert.");
    }

    private void MenuMoveUp_Click(object sender, RoutedEventArgs e) => Move(EntryOf(sender), -1);

    private void MenuMoveDown_Click(object sender, RoutedEventArgs e) => Move(EntryOf(sender), +1);

    private void Move(ModEntry? mod, int delta)
    {
        if (mod == null) return;
        int from = _mods.IndexOf(mod), to = from + delta;
        if (from < 0 || to < 0 || to >= _mods.Count) return;
        _mods.Move(from, to);
        Persist();
    }

    private void MenuRemove_Click(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { IsSuggestion: false } mod) return;
        bool subscribed = SteamService.IsModDownloaded(_data.AppId, mod.Id) == true;
        if (!Ask($"„{mod.Title}“ aus der Liste entfernen?\n\n(Die Mod selbst bleibt in Steam abonniert" +
                 (subscribed ? " und wird nicht mehr automatisch hinzugefügt.)" : ".)"))) return;

        _mods.Remove(mod);
        if (subscribed && !_data.RemovedSubscriptions.Contains(mod.Id))
            _data.RemovedSubscriptions.Add(mod.Id);
        // Bild behalten, wenn die Mod wieder unter „Beliebte Mods“ erscheint.
        if (!Suggestions.Contains(mod.Id))
        {
            mod.Preview = null;
            ModStore.DeleteImage(mod.Id);
        }
        Persist();
        RebuildSuggestions();
        SetStatus($"„{mod.Title}“ entfernt.");
    }
}
