# HuntXtract

by Chris aka BliZzi1337 · [Steam](https://steamcommunity.com/id/blizzi1337/)

Match-Tracker für Hunt: Showdown. Liest nach jedem Match die Mission Summary aus – auf Wunsch
auch die Team Details mit MMR und Mitspielern – und führt Buch über Kills, Bounty, Hunt Dollars,
Extraction Rate und MMR, auch pro Karte. Das Bild kommt direkt vom Bildschirm oder aus einer
OBS-Spielaufnahme. Dazu ein Countdown-Fenster im Spiel, frei gestaltbare Overlays für den Stream
und sechs Designs für das Programm. Alles läuft lokal, ohne Eingriff ins Spiel. Auf Englisch und
Deutsch – neue Installationen starten auf Englisch, die Sprache wird im ersten Schritt der Tour
gewählt.

## Bauen

Nur Windows 10/11, kein SDK: `build.ps1` kompiliert mit dem C#-Compiler von .NET Framework 4.8
nach `out\HuntXtract\`.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

## Aufbau

- `src/` – das Programm (C#): Bildquelle (OBS oder Bildschirm), Wiedererkennung, Texterkennung
  mit Windows OCR, Datenhaltung, Fenster; `src/MapTrail.cs` – die Tab-Karte: gelben Punkt
  finden, Zoom am roten Rand oder an gemerkten Kartenbildern erkennen, Laufspur des
  laufenden Matches sammeln; `src/HuntLog.cs` – liest Hunts eigenes Protokoll (`USER\Game.log`,
  über Steam gefunden) für Menü/Match/Karte mit und den Steam-Namen aus `loginusers.vdf`
  (für Tests liest `KOPFGELD_TEST_SCREEN` ein Bild statt des Monitors, `KOPFGELD_TEST_HUNTLOG`
  eine Datei statt des Hunt-Protokolls)
- `web/` – die Oberfläche (HTML/JS/CSS in WebView2) und das OBS-Overlay
  - `web/app/i18n.js` – die englischen Texte (im Code steht Deutsch; `tr`/`t` schlagen nach),
    Flaggen der Sprachauswahl; im C#-Teil stehen beide Sprachen direkt nebeneinander (`Lang.T`)
  - `web/app/themes.css` – die Designs, `web/overlay/overlay.js` – Overlay-Vorlagen und -Darstellung
  - `web/fonts/` – Crimson Text, EB Garamond, Rye (SIL Open Font License, Lizenztexte daneben)
  - `web/img/` – Hintergrundbilder aus dem offiziellen Hunt-Fankit von Crytek
- `art/logo.webp` – das HuntXtract-Logo; `tools/make-icon.ps1` macht daraus `app.ico` und das Zeichen
  in der Seitenleiste (`web/img/logo-mark.png`)
- `tools/make-templates.ps1` – erzeugt aus einer fertigen Einrichtung die Beispielbilder
  (`web/img/vorlage-*.jpg`), Spielernamen verpixelt
- `tools/make-preset.ps1` – erzeugt aus einer fertigen Einrichtung in 1920 × 1080 die Vorgabe
  für neue Nutzer (`web/vorgabe/`, für andere Spielsprachen `web/vorgabe/<sprache>/` wie
  `german`: Kalibrierung und saubere Bilder der drei Seiten, Namen verpixelt). `src/Presets.cs`
  übernimmt sie beim ersten Start, wenn Hunt laut `USER\game.cfg` in 16:9 ab 1920 × 1080 läuft
  und es für die Sprache aus Hunts Protokoll eine Vorgabe gibt
- `vendor/` – die WebView2-Bibliotheken aus Microsofts NuGet-Paket
- `test/` – Testwerkzeuge: nachgebautes OBS (MockObs), OCR-Tests, DevTools-Helfer
- `LIESMICH.txt` / `README.txt` – die Anleitung für Nutzer (Deutsch / Englisch)

Fan-Projekt, nicht mit Crytek verbunden.
