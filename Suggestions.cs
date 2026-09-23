namespace EaWModLauncher;

/// <summary>
/// Vorgeschlagene Mods: die meistabonnierten vollständigen Mods für Forces of Corruption im Steam Workshop
/// (Stand September 2026). Submods, reine Balance-Tweaks, veraltete Versionen und reine EaW-Mods sind ausgelassen.
/// Der Titel dient nur als Platzhalter, bis Name und Bild vom Workshop geladen sind.
/// </summary>
public static class Suggestions
{
    public static readonly (string Id, string Title)[] Popular =
    {
        ("1125571106", "EaWX: Thrawn's Revenge"),
        ("1976399102", "Empire at War Expanded: Fall of the Republic"),
        ("1129810972", "Republic at War"),
        ("1770851727", "Empire at War: Remake"),
        ("1397421866", "Awakening of the Rebellion"),
        ("3417277973", "Empire at War Expanded: Revan's Revenge"),
        ("3387971722", "Awakening of the Clone Wars"),
        ("1126673817", "The Clone Wars"),
        ("1156943126", "Ultimate Galactic Conquest Custom Edition"),
        ("1780988753", "Rise of the Mandalorians: Definitive Edition"),
        ("1463042552", "Age of Legends"),
        ("1125764259", "Star Wars Battlefront Commander"),
        ("1130150761", "Old Republic at War"),
    };

    public static bool Contains(string id) => Popular.Any(p => p.Id == id);
}
