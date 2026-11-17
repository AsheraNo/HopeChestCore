using System.Globalization;
using System.Windows;

namespace HopeChestCore;

internal static class UiText
{
    public static CultureInfo For(string? code) =>
        CultureInfo.GetCultureInfo((code ?? CultureInfo.CurrentUICulture.Name).StartsWith("de", StringComparison.OrdinalIgnoreCase)
            ? "de-DE" : "en-GB");

    private static readonly Dictionary<string, (string English, string German)> Labels = new()
    {
        ["Dashboard"] = ("Dashboard", "Übersicht"),
        ["Luck"] = ("Luck", "Glück"),
        ["ReplayImport"] = ("REPLAY IMPORT", "REPLAY-IMPORT"),
        ["ReplayFolder"] = ("Replay folder", "Replay-Ordner"),
        ["ScanNow"] = ("Scan now", "Jetzt prüfen"),
        ["Statistics"] = ("STATISTICS", "STATISTIK"),
        ["Reset"] = ("Reset statistics", "Statistik zurücksetzen"),
        ["ImportStatus"] = ("IMPORT STATUS", "IMPORTSTATUS"),
        ["PeriodLabel"] = ("PERIOD", "ZEITRAUM"),
        ["PeriodName"] = ("Period", "Zeitraum"),
        ["ChestStats"] = ("Chest statistics", "Truhenstatistik"),
        ["Total"] = ("Total chests", "Truhen gesamt"),
        ["Own"] = ("Own chests", "Eigene Truhen"),
        ["Team"] = ("Team chests", "Teammate-Truhen"),
        ["Matches"] = ("Matches tracked", "Erfasste Matches"),
        ["TotalHelp"] = ("Your opened and destroyed chests plus team chests recorded in the replay.", "Deine geöffneten und zerstörten Truhen plus die im Replay erfassten Teamtruhen."),
        ["OwnHelp"] = ("Chests you opened or destroyed.", "Deine geöffneten und zerstörten Truhen."),
        ["TeamHelp"] = ("Your teammates’ chests, where recorded in the replay.", "Truhen deiner Teammates, soweit sie im Replay erfasst wurden."),
        ["PerMatch"] = ("Chests / match", "Truhen / Match"),
        ["PerMinute"] = ("Chests / minute", "Truhen / Minute"),
        ["ChartTitle"] = ("Chest history", "Truhenverlauf"),
        ["TotalLegend"] = ("●  Total chests", "●  Truhen gesamt"),
        ["OwnLegend"] = ("●  Own chests", "●  Eigene Truhen"),
        ["MatchSuffix"] = (" / match  ·  ", " / Match  ·  "),
        ["MinuteSuffix"] = (" / min.", " / Min."),
        ["ChartAccess"] = ("History of total and own chests", "Verlauf der gesamten und eigenen Truhen"),
        ["DropOdds"] = ("Drop odds", "Drop-Chancen"),
        ["AtLeastOne"] = ("Chance of at least one drop", "Chance auf mindestens einen Treffer"),
        ["TotalCount"] = ("{0:N0} total chests", "{0:N0} Truhen gesamt"),
        ["BaseOdds"] = ("1 in {0:N0} per chest", "1 : {0:N0} pro Truhe"),
        ["ModelNote"] = ("Estimate using fixed base odds. Event boosts and loot availability are excluded.", "Schätzung mit festen Basisquoten. Event-Boni und Loot-Zeiträume sind nicht eingerechnet."),
        ["outfit"] = ("Weekly Golden Outfit", "Wöchentliches Goldenes Outfit"),
        ["pirate-shop"] = ("Pirate Shop Access", "Zugang zum Piratenshop"),
        ["car"] = ("Weekly Vehicle & Decal", "Wöchentliches Fahrzeug & Dekal"),
        ["pickaxe"] = ("Weekly Pickaxe or Back Bling", "Wöchentliche Spitzhacke oder Rücken-Accessoire"),
        ["jam"] = ("Weekly Jam Track", "Wöchentlicher Jam Track"),
        ["loading"] = ("Weekly Loading Screen", "Wöchentlicher Ladebildschirm"),
        ["gizmo"] = ("Gizmo (any)", "Gizmo (beliebig)"),
        ["HistoryTitle"] = ("Match history", "Match-Verlauf"),
        ["DateColumn"] = ("Match time", "Matchzeit"),
        ["PlayerColumn"] = ("Player", "Spieler"),
        ["ModeColumn"] = ("Mode", "Modus"),
        ["TotalColumn"] = ("Total", "Gesamt"),
        ["OwnColumn"] = ("Own", "Eigene"),
        ["TeamColumn"] = ("Team", "Teammates"),
        ["LengthColumn"] = ("Duration", "Dauer"),
        ["TotalRateColumn"] = ("Total / min.", "Ges. / Min."),
        ["OwnRateColumn"] = ("Own / min.", "Eig. / Min."),
        ["NoteColumn"] = ("Note", "Hinweis"),
        ["VehicleMissing"] = ("Vehicle events missing", "Fahrzeugereignisse fehlen"),
        ["ReplayPlayer"] = ("Replay player", "Replay-Spieler"),
        ["HistoryEmpty"] = ("No matches in this period.", "Keine Matches in diesem Zeitraum."),
        ["ChartEmpty"] = ("No matches yet.", "Noch keine Matches."),
        ["NoDuration"] = ("Replay duration unavailable.", "Keine Replay-Dauer verfügbar."),
        ["ChartTooltip"] = ("{0:g} · {1}\nTotal {2} · Own {3} {4}", "{0:g} · {1}\nGesamt {2} · Eigene {3} {4}"),
        ["UnitChests"] = ("chests", "Truhen"),
        ["UnitPerMinute"] = ("chests / min.", "Truhen / Min."),
        ["Open"] = ("Open", "Öffnen"),
        ["Exit"] = ("Exit", "Beenden"),
        ["ChooseFolder"] = ("Select the Fortnite replay folder", "Ordner mit Fortnite-Replays auswählen"),
        ["FolderError"] = ("Could not save the folder. {0}", "Ordner konnte nicht gespeichert werden. {0}"),
        ["ResetConfirm"] = ("Permanently delete saved matches, import entries, and Luck entries? HopeChestCore will count matches from the reset onward. Your replay files stay on disk.", "Gespeicherte Matches, Import-Einträge und Glücks-Einträge endgültig löschen? HopeChestCore zählt danach Matches ab dem Reset. Deine Replay-Dateien bleiben auf der Festplatte."),
        ["ResetOk"] = ("History cleared. Waiting for new matches.", "Verlauf gelöscht. Warte auf neue Matches."),
        ["ResetError"] = ("Could not reset statistics.\n\n{0}", "Die Statistik konnte nicht zurückgesetzt werden.\n\n{0}"),
        ["Scanning"] = ("Checking replays …", "Replays werden geprüft …"),
        ["MissingFolder"] = ("Replay folder missing. Enable replay recording in Fortnite or choose a folder.", "Replay-Ordner fehlt. Aktiviere in Fortnite „Replays aufzeichnen“ oder wähle deinen Ordner."),
        ["NoReplays"] = ("No replays found. Enable replay recording in Fortnite and finish a match.", "Keine Replays gefunden. Aktiviere in Fortnite „Replays aufzeichnen“ und beende ein Match."),
        ["PendingImports"] = ("Auto import active · {0:N0} replay(s) awaiting completion.", "Automatischer Import aktiv · {0:N0} Aufnahme(n) warten auf Abschluss."),
        ["ActiveImport"] = ("Auto import active · Checking the replay folder every 12 seconds.", "Automatischer Import aktiv · Der Replay-Ordner wird alle 12 Sekunden geprüft."),
        ["ImportError"] = ("Import interrupted. {0}", "Import unterbrochen. {0}"),
        ["Issues"] = ("Skipped replays ({0:N0})", "Übersprungene Replays ({0:N0})"),
        ["AllDays"] = ("All days", "Alle Tage"),
        ["Today"] = ("Today", "Heute"),
        ["RateDuration"] = ("Per-minute rates use replay duration", "Berechnung pro Minute anhand der Replay-Dauer"),
        ["SomeDurations"] = ("Replay duration available for {0} of {1} matches", "Replay-Dauer verfügbar für {0} von {1} Matches"),
        ["OneMatch"] = ("1 match", "1 Match"),
        ["LastMatches"] = ("Last {0} matches", "Letzte {0} Matches"),
        ["AlreadyOpen"] = ("HopeChestCore is already running. Open it from its icon in the notification area.", "HopeChestCore läuft bereits. Das Fenster lässt sich über das Symbol im Infobereich öffnen."),
        ["StartupError"] = ("HopeChestCore could not start.\n\n{0}", "HopeChestCore konnte nicht gestartet werden.\n\n{0}"),
        ["LanguageError"] = ("Could not save the language preference. {0}", "Die Spracheinstellung konnte nicht gespeichert werden. {0}"),
        ["RecordingPending"] = ("Waiting for Fortnite to finish the recording.", "Aufnahme wartet auf Abschluss durch Fortnite."),
        ["OwnerMissing"] = ("The player who recorded the replay could not be identified.", "Der Spieler, der das Replay aufgenommen hat, konnte nicht zugeordnet werden."),
        ["UnsupportedMode"] = ("Unsupported mode: {0}", "Nicht unterstützter Modus: {0}"),
        ["Unknown"] = ("unknown", "unbekannt"),
        ["ReplayReadError"] = ("Could not read the replay. {0}", "Replay konnte nicht gelesen werden. {0}"),
        ["InvalidHeader"] = ("Invalid replay header.", "Ungültiger Replay-Dateikopf."),
        ["UnknownHeader"] = ("Unknown replay header.", "Unbekannter Replay-Dateikopf."),
        ["ParserStart"] = ("Could not start the replay analysis.", "Replay-Analyse konnte nicht starten."),
        ["ParserTimeout"] = ("Replay analysis timed out.", "Zeitlimit für die Replay-Analyse erreicht."),
        ["ParserExit"] = ("The replay parser stopped unexpectedly.", "Der Replay-Parser wurde unerwartet beendet."),
        ["ParserEmpty"] = ("The replay parser returned no result.", "Der Replay-Parser lieferte kein Ergebnis."),
        ["LuckTitle"] = ("How lucky were your drops?", "Wie viel Glück hattest du?"),
        ["LuckIntro"] = ("Enter how often the reward dropped during the selected period. See the odds of getting at least that many.", "Gib ein, wie oft die Belohnung im gewählten Zeitraum gedroppt ist. Du siehst die Chance auf mindestens so viele Funde."),
        ["Reward"] = ("Reward", "Belohnung"),
        ["YourLuck"] = ("Your luck", "Dein Glück"),
        ["LuckInput"] = ("Enter a whole number of 0 or more.", "Gib eine ganze Zahl ab 0 ein."),
        ["TooManyFinds"] = ("The count exceeds the total chests in this period.", "Die Anzahl übersteigt die Truhen gesamt in diesem Zeitraum."),
        ["RoundedLuck"] = ("≈ 1 in {0} or {1}", "≈ 1 zu {0} oder {1}"),
        ["CombinedLuck"] = ("How lucky are you really?", "Wie viel Glück hattest du wirklich?"),
        ["LuckComparison"] = ("At least as rare as “{0}” · 1 in {1}", "Mindestens so selten wie „{0}“ · 1 zu {1}"),
        ["LuckComparisonEstimate"] = ("At least as rare as “{0}” · estimated 1 in {1}", "Mindestens so selten wie „{0}“ · geschätzt 1 zu {1}"),
        ["CombinedAssumption"] = ("At least every entered count. The estimate uses total chests for each row and treats the rows as independent.", "Mindestens alle eingegebenen Fundzahlen. Die Schätzung verwendet pro Zeile alle Truhen und nimmt unabhängige Zeilen an."),
        ["LuckModelNote"] = ("Only entered boosts are applied. Actual Treasure Hour exposure and loot availability are excluded.", "Es gelten nur die eingegebenen Boosts. Die tatsächliche Anzahl an Event-Truhen und die Verfügbarkeit der Beute bleiben unberücksichtigt."),
        ["NoCombinedFinds"] = ("Add a find to see your combined odds.", "Füge einen Fund hinzu, um deine Gesamtchance zu sehen."),
        ["InvalidCombinedFinds"] = ("Check the highlighted inputs.", "Prüfe die markierten Eingaben."),
        ["AddFind"] = ("Add one find", "Einen Fund hinzufügen"),
        ["RegularFinds"] = ("Regular finds", "Normale Funde"),
        ["TreasureFinds"] = ("Treasure Hour finds", "Funde in der Treasure Hour"),
        ["AddTreasureFind"] = ("Add one Treasure Hour find", "Einen Treasure-Hour-Fund hinzufügen"),
        ["TreasureHour"] = ("Treasure Hour", "Treasure Hour"),
        ["TreasureHourHelp"] = ("Record boosted finds here and regular finds in the table below. Add a boost row for each different multiplier.", "Trage Event-Funde hier und normale Funde unten ein. Füge für jeden weiteren Multiplikator eine Boost-Zeile hinzu."),
        ["BoostMultiplier"] = ("Multiplier (×)", "Multiplikator (×)"),
        ["AddBoost"] = ("Add boost", "Boost hinzufügen"),
        ["RemoveBoost"] = ("Remove boost row", "Boost-Zeile entfernen"),
        ["InvalidMultiplier"] = ("Enter a multiplier from 1 to {0:N0}.", "Gib einen Multiplikator von 1 bis {0:N0} ein."),
        ["TooManyCombinedFinds"] = ("The combined finds for a reward exceed total chests.", "Die zusammengefassten Funde einer Belohnung übersteigen die Gesamtzahl der Truhen."),
        ["LuckSaveFailed"] = ("Could not save Luck entries. {0}", "Glücks-Einträge konnten nicht gespeichert werden. {0}"),
    };

    public static ResourceDictionary CreateResources(CultureInfo culture)
    {
        var resources = new ResourceDictionary();
        foreach (var (key, _) in Labels)
            resources.Add(key, Get(culture, key));
        return resources;
    }

    public static string Get(CultureInfo culture, string key, params object[] arguments)
    {
        var labels = Labels[key];
        var text = culture.Name == "de-DE" ? labels.German : labels.English;
        return arguments.Length == 0 ? text : string.Format(culture, text, arguments);
    }
}
