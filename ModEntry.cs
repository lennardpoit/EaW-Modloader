using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace EaWModLauncher;

/// <summary>Eine Workshop-Mod – entweder vom Nutzer hinzugefügt oder ein Vorschlag.</summary>
public class ModEntry : INotifyPropertyChanged
{
    private string _title = "";
    private ImageSource? _preview;
    private bool _isDownloaded = true;

    public string Id { get; set; } = "";

    public string Title
    {
        get => _title;
        set { _title = value; OnChanged(); }
    }

    /// <summary>Dateiname des zwischengespeicherten Vorschaubilds im Bildordner.</summary>
    public string? ImageFile { get; set; }

    [JsonIgnore]
    public ImageSource? Preview
    {
        get => _preview;
        set { _preview = value; OnChanged(); OnChanged(nameof(HasPreview)); }
    }

    [JsonIgnore]
    public bool HasPreview => _preview != null;

    /// <summary>true = Eintrag aus „Beliebte Mods“, false = vom Nutzer hinzugefügt.</summary>
    [JsonIgnore]
    public bool IsSuggestion { get; init; }

    [JsonIgnore]
    public bool IsOwn => !IsSuggestion;

    /// <summary>Mod-Ordner im Workshop-Verzeichnis vorhanden (bzw. nicht prüfbar).</summary>
    [JsonIgnore]
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (_isDownloaded == value) return;
            _isDownloaded = value;
            OnChanged(); OnChanged(nameof(IsMissing)); OnChanged(nameof(HoverText));
        }
    }

    [JsonIgnore]
    public bool IsMissing => !_isDownloaded;

    [JsonIgnore]
    public string HoverText => Loc.T(_isDownloaded ? "TileStart" : "TileSubscribe");

    /// <summary>Nach einem Sprachwechsel die sprachabhängigen Texte neu anzeigen.</summary>
    public void RefreshTexts() => OnChanged(nameof(HoverText));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Inhalt der gespeicherten Konfigurationsdatei.</summary>
public class LauncherData
{
    /// <summary>Steam-App-ID von Star Wars Empire at War: Gold Pack.</summary>
    public int AppId { get; set; } = 32470;

    /// <summary>Sprache der Oberfläche: "en" (Standard) oder "de".</summary>
    public string Language { get; set; } = "en";

    public List<ModEntry> Mods { get; set; } = new();

    /// <summary>Zwischengespeicherte Namen/Bilder der vorgeschlagenen Mods (für den Offline-Betrieb).</summary>
    public List<ModEntry> SuggestionCache { get; set; } = new();

    /// <summary>Vorschläge, die der Nutzer ausgeblendet hat.</summary>
    public List<string> HiddenSuggestions { get; set; } = new();

    /// <summary>Abonnierte Mods, die der Nutzer aus „Meine Mods“ entfernt hat – werden nicht erneut automatisch hinzugefügt.</summary>
    public List<string> RemovedSubscriptions { get; set; } = new();
}
