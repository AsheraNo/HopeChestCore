# HopeChestCore

HopeChestCore ist eine Windows-App, die nach dem Match Truhen aus lokal gespeicherten Fortnite-Replays zählt. Sie unterstützt reguläres Battle Royale und Zero Build.

[**Windows-ZIP v0.4.0 herunterladen**](https://github.com/AsheraNo/HopeChestCore/releases/download/v0.4.0/HopeChestCore_v0.4.0.zip) · [Alle Releases](https://github.com/AsheraNo/HopeChestCore/releases)

## Fair Play und Zugriff auf Fortnite

**HopeChestCore öffnet `.replay`-Dateien ausschließlich zum Lesen.** Die App verändert oder löscht keine Replays, Spieldateien, Fortnite-Einstellungen oder Kontodaten. Sie greift weder auf den laufenden Fortnite-Prozess noch auf dessen Speicher zu, sendet keine Eingaben ans Spiel und bietet keine Live-Spielhilfe. Ein Epic-Konto oder ein Login in der App ist nicht erforderlich.

## Datenschutz

HopeChestCore verarbeitet Replays offline auf deinem PC. Die App sendet weder Replays noch Statistiken oder Telemetriedaten an den Entwickler oder an Dritte. Matchstatistiken (unter anderem der Spielername aus dem Replay), Importinformationen und der gewählte Replay-Ordner werden in der lokalen SQLite-Datenbank unter `%LOCALAPPDATA%\HopeChestCore\history.db` gespeichert. Während der Auswertung verwendet HopeChestCore eine temporäre Ergebnisdatei und räumt sie nach der Verarbeitung auf.

## Schnellstart

1. Installiere die [.NET 10 Desktop Runtime für Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Lade die ZIP-Datei aus dem Release herunter, entpacke sie vollständig und starte `HopeChestCore.exe`.
3. Aktiviere in Fortnite unter **Einstellungen → Spiel → Replays** die Replay-Aufzeichnung.
4. HopeChestCore importiert abgeschlossene Aufnahmen automatisch. Standardmäßig sucht die App unter `%LOCALAPPDATA%\FortniteGame\Saved\Demos`. Unter **Replay-Ordner wählen** kannst du einen anderen Ordner auswählen.

Beim Minimieren läuft HopeChestCore im Windows-Infobereich weiter. Ein Doppelklick aufs Symbol öffnet das Fenster. Das Schließen des Fensters beendet die App.

## So funktioniert die Auswertung

1. HopeChestCore prüft den Replay-Ordner alle 12 Sekunden und wartet, bis die jeweilige Aufnahme abgeschlossen ist.
2. Ein lokaler Replay-Parser zählt eigene geöffnete und zerstörte Truhen sowie die im Replay erfassten Truhen deiner Teammitglieder. Munitionskisten werden ausgelassen.
3. HopeChestCore nutzt die Fortnite-Session-ID und einen SHA-256-Hash der Replay-Datei, um doppelte Importe zu erkennen. Kopien derselben Aufnahme zählen als ein Match. Beim Update auf v0.4.0 werden vorhandene Einträge mit derselben Session-ID zusammengeführt.

Das Dashboard zeigt eigene Truhen, Truhen der Teammitglieder, die Gesamtsumme und die Zahl der erfassten Matches. Für deine eigenen Truhen und die Gesamtsumme gibt es Durchschnittswerte pro Match und pro Minute sowie Diagramme für die letzten 20 Matches. Beide Diagramme lassen sich gemeinsam zwischen den beiden Ansichten umschalten.

Der Filter **Zeitraum** gilt für das gesamte Dashboard samt Match-Verlauf. Du kannst zwischen **Heute**, **Alle Tage** und einem Datum aus dem Verlauf wählen. Maßgeblich ist der lokale Kalendertag des Matchstarts.

Truhen pro Minute werden anhand der Replay-Dauer berechnet. Matches ohne verfügbare Dauer zählen zur Gesamtzahl und zum Durchschnitt pro Match. Falls das Replay noch vorhanden ist, kann die App die Dauer nachtragen.

**Statistik zurücksetzen** löscht Matches und Import-Einträge aus der lokalen Datenbank. Anschließend zählt HopeChestCore wieder ab dem Zeitpunkt des Resets.

## Hinweise und Disclaimer

HopeChestCore ist ein inoffizielles Fan-Tool und steht in keiner Verbindung zu Epic Games, Inc. Fortnite und zugehörige Marken sowie urheberrechtlich geschützte Inhalte von Epic gehören Epic Games, Inc. Alle Rechte daran bleiben Epic vorbehalten. Epic hat HopeChestCore weder genehmigt noch unterstützt. Die Nutzung erfolgt auf eigene Verantwortung. [Richtlinie von Epic Games für Fan-Inhalte](https://legal.epicgames.com/epicgames/fan-art-policy?lang=de)

Die Statistik hängt davon ab, welche Ereignisse Fortnite im Replay aufgezeichnet hat. Ereignisse außerhalb deiner Nähe können fehlen, insbesondere bei Teammitgliedern. Änderungen am Replay-Format können die Auswertung beeinträchtigen und ein Update des Parsers erfordern.

## Quellcode und Lizenzen

Der C#-Quellcode, die Build-Anleitung und die Lizenztexte liegen im Ordner `Quellcode` der Windows-ZIP.

Die Replay-Analyse basiert auf Fortnite-ChestCounter 1.5.1 (LED-N, MIT, Stand `1ec3636`) und dessen Fork von FortniteReplayDecompressor (Shiqan, MIT). Die MIT-Lizenztexte liegen dort unter `vendor/`.

Der enthaltene OozSharp-Dekompressor übernimmt Code von Powzix unter GPL-3.0-or-later. Die kombinierte Fassung von HopeChestCore wird unter GPL-3.0-or-later bereitgestellt. Der GPL-Lizenztext liegt unter `Quellcode/vendor/LICENSE-OozSharp-GPL-3.0` in der Windows-ZIP.
