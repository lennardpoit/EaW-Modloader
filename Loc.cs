using System.ComponentModel;

namespace EaWModLauncher;

/// <summary>
/// Übersetzungen (Englisch/Deutsch). In XAML: {Binding [Key], Source={x:Static local:Loc.Instance}},
/// im Code: Loc.T("Key", args). Beim Umschalten der Sprache aktualisieren sich alle Bindungen sofort.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    /// <summary>"en" (Standard) oder "de".</summary>
    public static string Language { get; private set; } = "en";

    public string this[string key] => Get(key);

    public static string T(string key, params object[] args) =>
        args.Length == 0 ? Get(key) : string.Format(Get(key), args);

    public static void SetLanguage(string? language)
    {
        Language = language == "de" ? "de" : "en";
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string Get(string key) =>
        Strings.TryGetValue(key, out var s) ? (Language == "de" ? s.De : s.En) : key;

    private static readonly Dictionary<string, (string En, string De)> Strings = new()
    {
        // Fenster und Kopfzeile
        ["WindowTitle"] = ("Forces of Corruption – Mod Launcher", "Forces of Corruption – Mod-Launcher"),
        ["HeaderSubtitle"] = ("MOD LAUNCHER", "MOD-LAUNCHER"),
        ["InputHint"] = ("Paste Workshop ID or link …", "Workshop-ID oder Link einfügen …"),
        ["AddButton"] = ("+ ADD", "+ HINZUFÜGEN"),
        ["PlayVanilla"] = ("PLAY WITHOUT MODS", "OHNE MOD STARTEN"),
        ["PlayVanillaTip"] = ("Start Forces of Corruption without mods", "Forces of Corruption ohne Mod starten"),

        // Update-Hinweis
        ["UpdateNow"] = ("UPDATE NOW", "JETZT AKTUALISIEREN"),
        ["WhatsNew"] = ("What's new?", "Was ist neu?"),
        ["RemindLater"] = ("Remind me later", "Später erinnern"),
        ["UpdateAvailable"] = ("New version {0} available (installed: {1}).", "Neue Version {0} verfügbar (installiert: {1})."),
        ["UpdateDownloading"] = ("Downloading version {0} … {1:P0}", "Lade Version {0} herunter … {1:P0}"),
        ["UpdateInstalled"] = ("Version {0} installed – restarting …", "Version {0} installiert – Neustart …"),
        ["UpdateFailed"] = (
            "The automatic update failed:\n{0}\n\nOpen the download page in your browser to download the new version manually?",
            "Das automatische Update ist fehlgeschlagen:\n{0}\n\nDownload-Seite im Browser öffnen, um die neue Version von Hand herunterzuladen?"),

        // Abschnitte
        ["MyMods"] = ("MY MODS", "MEINE MODS"),
        ["MyModsInfo"] = (
            "All mods you have subscribed to on the Steam Workshop appear here automatically.",
            "Alle Mods, die du im Steam Workshop abonniert hast, erscheinen hier automatisch."),
        ["MyModsEmpty"] = (
            "No mods subscribed yet. Subscribe to one of the popular mods below or paste a Workshop ID or link above.",
            "Noch keine Mods abonniert. Abonniere unten eine der beliebten Mods oder füge oben eine Workshop-ID bzw. einen Link ein."),
        ["PopularMods"] = ("POPULAR MODS", "BELIEBTE MODS"),
        ["PopularInfo"] = (
            "The most subscribed mods on the Steam Workshop that you don't have yet. A click opens the Workshop page so you can subscribe – the mod then appears under “My Mods” automatically.",
            "Die meistabonnierten Mods im Steam Workshop, die du noch nicht hast. Ein Klick öffnet die Workshop-Seite zum Abonnieren – danach erscheint die Mod automatisch unter „Meine Mods“."),
        ["ShowHidden"] = ("Show hidden ({0})", "Ausgeblendete anzeigen ({0})"),
        ["Counts"] = ("{0} installed · {1} suggested · v{2}", "{0} eigene · {1} vorgeschlagen · v{2}"),

        // Kacheln und Kontextmenü
        ["NoPreview"] = ("No preview image", "Kein Vorschaubild"),
        ["NotSubscribed"] = ("NOT SUBSCRIBED", "NICHT ABONNIERT"),
        ["TileStart"] = ("▶  PLAY", "▶  STARTEN"),
        ["TileSubscribe"] = ("SUBSCRIBE ON WORKSHOP", "IM WORKSHOP ABONNIEREN"),
        ["MenuStart"] = ("Launch", "Starten"),
        ["MenuAdopt"] = ("Add to “My Mods”", "Zu „Meine Mods“ hinzufügen"),
        ["MenuRefresh"] = ("Refresh name and image", "Name und Bild aktualisieren"),
        ["MenuWorkshop"] = ("Open Workshop page", "Workshop-Seite öffnen"),
        ["MenuCopy"] = ("Copy launch option", "Startoption kopieren"),
        ["MenuMoveUp"] = ("Move forward", "Nach vorne verschieben"),
        ["MenuMoveDown"] = ("Move back", "Nach hinten verschieben"),
        ["MenuRemove"] = ("Remove", "Entfernen"),
        ["MenuHide"] = ("Hide suggestion", "Vorschlag ausblenden"),

        // Statusmeldungen und Dialoge
        ["Ready"] = (
            "Ready. Click a tile to launch the mod, right-click for more options.",
            "Bereit. Klick auf eine Kachel startet die Mod, Rechtsklick für weitere Optionen."),
        ["SubsAddedOne"] = ("1 newly subscribed mod was added to “My Mods”.", "1 neu abonnierte Mod wurde zu „Meine Mods“ hinzugefügt."),
        ["SubsAddedMany"] = ("{0} newly subscribed mods were added to “My Mods”.", "{0} neu abonnierte Mods wurden zu „Meine Mods“ hinzugefügt."),
        ["LoadingDetails"] = ("Loading names and images from the Steam Workshop …", "Lade Namen und Bilder aus dem Steam Workshop …"),
        ["DetailsFailed"] = (
            "Couldn't load names and images from the Steam Workshop (offline?). Will retry on next start.",
            "Namen und Bilder konnten nicht vom Steam Workshop geladen werden (offline?). Beim nächsten Start wird es erneut versucht."),
        ["SaveFailed"] = ("The mod list couldn't be saved:\n{0}", "Die Mod-Liste konnte nicht gespeichert werden:\n{0}"),
        ["InvalidId"] = (
            "Please enter a valid Workshop ID (digits only) or a Workshop link.\n\nExample: 1125571106 or https://steamcommunity.com/sharedfiles/filedetails/?id=1125571106",
            "Bitte eine gültige Workshop-ID (nur Ziffern) oder einen Workshop-Link eingeben.\n\nBeispiel: 1125571106 oder https://steamcommunity.com/sharedfiles/filedetails/?id=1125571106"),
        ["AlreadyInList"] = ("The mod {0} is already in the list.", "Die Mod {0} ist bereits in der Liste."),
        ["LoadingMod"] = ("Loading info for mod {0} from the Steam Workshop …", "Lade Informationen zu Mod {0} vom Steam Workshop …"),
        ["WrongGame"] = (
            "According to Steam, “{0}” doesn't belong to Empire at War (app {1}).\n\nAdd it anyway?",
            "„{0}“ gehört laut Steam nicht zu Empire at War (App {1}).\n\nTrotzdem hinzufügen?"),
        ["AddCancelled"] = ("Adding cancelled.", "Hinzufügen abgebrochen."),
        ["WorkshopOffline"] = (
            "The Steam Workshop can't be reached right now:\n{0}\n\nAdd the mod anyway without name and image? (You can load them later via right-click → “Refresh”.)",
            "Der Steam Workshop ist gerade nicht erreichbar:\n{0}\n\nMod trotzdem ohne Namen und Bild hinzufügen? (Später per Rechtsklick → „aktualisieren“ nachladen.)"),
        ["Added"] = ("“{0}” added.", "„{0}“ hinzugefügt."),
        ["AddedNotDownloaded"] = (
            " Note: the mod isn't downloaded yet – please subscribe on the Workshop.",
            " Hinweis: Die Mod ist noch nicht heruntergeladen – bitte im Workshop abonnieren."),
        ["AddFailed"] = ("Adding failed.", "Hinzufügen fehlgeschlagen."),
        ["Adopted"] = ("“{0}” added to “My Mods”.", "„{0}“ zu „Meine Mods“ hinzugefügt."),
        ["WorkshopOpened"] = (
            "Opened the Workshop page of “{0}”. Once subscribed and downloaded, a click launches the mod.",
            "Workshop-Seite von „{0}“ geöffnet. Nach dem Abonnieren und Herunterladen startet ein Klick die Mod."),
        ["Launching"] = ("Launching Forces of Corruption with “{0}” (STEAMMOD={1}) …", "Starte Forces of Corruption mit „{0}“ (STEAMMOD={1}) …"),
        ["LaunchingVanilla"] = ("Launching Forces of Corruption without mods …", "Starte Forces of Corruption ohne Mod …"),
        ["LaunchFailed"] = ("Forces of Corruption couldn't be started:\n{0}", "Forces of Corruption konnte nicht gestartet werden:\n{0}"),
        ["LaunchFailedStatus"] = ("Launch failed.", "Start fehlgeschlagen."),
        ["Hidden"] = ("Suggestion “{0}” hidden.", "Vorschlag „{0}“ ausgeblendet."),
        ["AllShown"] = ("All suggestions are shown again.", "Alle Vorschläge werden wieder angezeigt."),
        ["Refreshing"] = ("Refreshing “{0}” …", "Aktualisiere „{0}“ …"),
        ["Refreshed"] = ("“{0}” refreshed.", "„{0}“ aktualisiert."),
        ["RefreshFailed"] = ("Refresh failed:\n{0}", "Aktualisieren fehlgeschlagen:\n{0}"),
        ["RefreshFailedStatus"] = ("Refresh failed.", "Aktualisieren fehlgeschlagen."),
        ["Copied"] = ("Copied “STEAMMOD={0}” to the clipboard.", "„STEAMMOD={0}“ in die Zwischenablage kopiert."),
        ["RemoveConfirm"] = (
            "Remove “{0}” from the list?\n\n(The mod stays subscribed in Steam.)",
            "„{0}“ aus der Liste entfernen?\n\n(Die Mod selbst bleibt in Steam abonniert.)"),
        ["RemoveConfirmSubscribed"] = (
            "Remove “{0}” from the list?\n\n(The mod stays subscribed in Steam and won't be added automatically again.)",
            "„{0}“ aus der Liste entfernen?\n\n(Die Mod selbst bleibt in Steam abonniert und wird nicht mehr automatisch hinzugefügt.)"),
        ["Removed"] = ("“{0}” removed.", "„{0}“ entfernt."),

        // Fehlermeldungen aus Steam- und Update-Funktionen
        ["ErrItemNotFound"] = (
            "Workshop item {0} was not found (wrong ID, private or deleted).",
            "Das Workshop-Item {0} wurde nicht gefunden (falsche ID, privat oder gelöscht)."),
        ["ErrSteamNotFound"] = ("Steam was not found on this PC.", "Steam wurde auf diesem PC nicht gefunden."),
        ["SteamStarting"] = ("Starting Steam – waiting for login …", "Steam wird gestartet – warte auf Anmeldung …"),
        ["ErrSteamNotReady"] = (
            "Steam isn't ready (not logged in?). Please open Steam, log in and try again.",
            "Steam ist nicht bereit (nicht angemeldet?). Bitte Steam öffnen, anmelden und erneut versuchen."),
        ["ErrGameNotFound"] = (
            "The Empire at War installation wasn't found in any Steam library.",
            "Die Installation von Empire at War wurde in keiner Steam-Bibliothek gefunden."),
        ["ErrFocNotFound"] = ("Forces of Corruption was not found:\n{0}", "Forces of Corruption wurde nicht gefunden:\n{0}"),
        ["ErrNoAsset"] = ("This release doesn't contain EaWModLauncher.exe.", "Dieses Release enthält keine EaWModLauncher.exe."),
        ["ErrNoPath"] = ("Program path unknown.", "Programmpfad unbekannt."),
        ["ErrDownloadCorrupt"] = ("The download is incomplete or corrupted.", "Der Download ist unvollständig oder beschädigt."),
    };
}
