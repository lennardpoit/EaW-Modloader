# EaW Mod-Launcher

Ein einfacher Mod-Launcher für **Star Wars: Empire at War – Forces of Corruption** (Steam-Version).
Alle abonnierten Workshop-Mods erscheinen als Kacheln mit Vorschaubild. Ein Klick startet das Spiel mit der
gewählten Mod, ganz ohne Rechtsklick → Eigenschaften → Startoptionen in Steam.

*English version below.*

## Funktionen

- **Abonnierte Mods automatisch:** Jede im Steam Workshop abonnierte Mod erscheint unter „Meine Mods“.
- **Ein Klick zum Spielen:** Startet Forces of Corruption direkt mit `STEAMMOD=<ID>`.
- **Beliebte Mods:** Vorschläge der meistabonnierten Workshop-Mods. Ein Klick öffnet die Workshop-Seite zum Abonnieren.
- **Mods per ID hinzufügen:** Workshop-ID oder Link einfügen, Name und Bild werden automatisch geladen.
- **Offline-fähig:** Namen und Vorschaubilder werden lokal zwischengespeichert.
- Startet Steam bei Bedarf automatisch im Hintergrund.
- **Automatische Updates:** Zeigt neue Versionen an und installiert sie mit einem Klick.

## [Download](https://github.com/lennardpoit/EaW-Modloader/releases/latest/download/EaWModLauncher.exe)

1. Unter **[Releases](../../releases)** die neueste `EaWModLauncher.exe` herunterladen.
2. In einen beliebigen Ordner legen und starten. Eine Installation ist nicht nötig.

**Hinweis zu Windows SmartScreen:** Beim ersten Start meldet Windows eventuell „Der Computer wurde durch Windows
geschützt“, weil das Programm nicht kostenpflichtig signiert ist. Dann auf **Weitere Informationen → Trotzdem
ausführen** klicken.

### Voraussetzungen

- Windows 10 oder 11 (64 Bit)
- Steam mit **STAR WARS™ Empire at War: Gold Pack**
- Eine .NET-Installation ist **nicht** nötig, sie ist in der `.exe` enthalten.

## Bedienung

| Aktion | Ergebnis |
| --- | --- |
| Klick auf eine Kachel | Startet Forces of Corruption mit dieser Mod |
| Klick auf eine nicht abonnierte Mod | Öffnet die Workshop-Seite zum Abonnieren |
| Rechtsklick auf eine Kachel | Aktualisieren, Workshop-Seite öffnen, Startoption kopieren, verschieben, entfernen |
| „Ohne Mod starten“ | Startet Forces of Corruption ohne Mod |

Wenn du eine abonnierte Mod aus „Meine Mods“ entfernst, fügt der Launcher sie nicht erneut automatisch hinzu.
Trägst du die Workshop-ID oben manuell ein, erscheint sie wieder.

### Wie wird das Spiel gestartet?

Steams eigene Startoption für das Gold Pack startet das Hauptspiel. Der Launcher ruft deshalb
`…\Star Wars Empire at War\corruption\StarWarsG.exe STEAMMOD=<ID>` direkt auf. Über die mitgelieferte
`steam_appid.txt` meldet sich das Spiel selbst bei Steam an und lädt die Workshop-Mod. Die Startoptionen im
Steam-Client werden dabei nicht verwendet.

### Updates

Beim Start prüft der Launcher, ob es auf GitHub eine neuere Version gibt. Wenn ja, erscheint oben ein Hinweis.
**Jetzt aktualisieren** lädt die neue Version herunter, ersetzt die alte `.exe` und startet den Launcher neu.
Die installierte Version steht unten rechts in der Statuszeile.

### Gespeicherte Daten

`%APPDATA%\EaWModLauncher\` enthält `mods.json` (Mod-Liste und Einstellungen) und `images\` (Vorschaubilder).
Zum Zurücksetzen einfach den Ordner löschen.

## Selbst bauen

Benötigt das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet publish -c Release
```

Erzeugt `EaWModLauncher.exe` im Projektordner. Das Programm-Icon liegt in `Assets\app.ico`, die Liste der
vorgeschlagenen Mods steht in `Suggestions.cs`.

### Neue Version veröffentlichen

1. In `EaWModLauncher.csproj` die `<Version>` erhöhen, z. B. `1.0.0` → `1.1.0`.
2. `dotnet publish -c Release` ausführen.
3. Auf GitHub ein neues Release anlegen: Tag **`v1.1.0`** (passend zur Version, mit `v` davor) und die Datei
   **`EaWModLauncher.exe`** unter genau diesem Namen anhängen.

Alle installierten Launcher zeigen die neue Version beim nächsten Start an.

## Rechtliches

Dies ist ein inoffizielles Fan-Projekt. Es steht in keiner Verbindung zu Lucasfilm, Disney, LucasArts,
Petroglyph oder Valve. STAR WARS, Empire at War und Forces of Corruption sind Marken von Lucasfilm Ltd.

Das Programm-Icon (`Assets/app.ico`) ist das offizielle Steam-Icon von *Star Wars: Empire at War* und gehört
Lucasfilm Ltd. Weitere Grafiken des Spiels sind nicht enthalten: Logo und Emblem werden zur Laufzeit aus der
lokalen Spielinstallation gelesen. Mod-Namen und Vorschaubilder stammen aus dem Steam Workshop und gehören den
jeweiligen Mod-Autoren.

## Lizenz

MIT, siehe [LICENSE](LICENSE).

---

## English

A simple mod launcher for **Star Wars: Empire at War – Forces of Corruption** (Steam). All subscribed Workshop
mods show up as tiles with preview images. One click launches the game with that mod (`STEAMMOD=<id>`), with no
need to edit launch options in Steam. It also suggests popular Workshop mods, lets you add mods by ID or link, and updates itself with one click when a new version is released.

**Download:** get the latest `EaWModLauncher.exe` from **[Releases](../../releases)** and run it. No installation or
.NET runtime required. If Windows SmartScreen appears, click *More info → Run anyway*.

**Requirements:** Windows 10/11 (64-bit), Steam, *STAR WARS™ Empire at War: Gold Pack*.

The user interface is currently in German. Unofficial fan project, not affiliated with Lucasfilm, Disney,
LucasArts, Petroglyph or Valve. The program icon is the official Steam icon of the game (© Lucasfilm Ltd.); no
other game assets are included, logo and emblem are read from your local game installation at runtime.
Code licensed under MIT.
