# HopeChestCore

HopeChestCore is a Windows app that counts chests from saved Fortnite replays after a match. It supports standard Battle Royale and Zero Build.

[**Download the Windows ZIP for v0.5.0**](https://github.com/AsheraNo/HopeChestCore/releases/download/v0.5.0/HopeChestCore_v0.5.0.zip) · [Browse the v0.5.0 source](https://github.com/AsheraNo/HopeChestCore/tree/v0.5.0) · [All releases](https://github.com/AsheraNo/HopeChestCore/releases)

The v0.5.0 download has a German interface.

## Fair play and Fortnite access

**HopeChestCore opens `.replay` files in read-only mode.** It does not change or delete replays, game files, Fortnite settings, or account data. It does not access the running Fortnite process or its memory, send inputs to the game, or offer live gameplay assistance. The app requires no Epic account or sign-in.

The replay file is opened in [`ReplayAnalysis.cs`](ReplayAnalysis.cs).

## Privacy

HopeChestCore processes replays offline on your PC. It sends no replays, statistics, or telemetry to the developer or any third party. Match statistics, including the player name from the replay, import records, and your chosen replay folder are stored in a local SQLite database at `%LOCALAPPDATA%\HopeChestCore\history.db`. The app deletes the temporary analysis output after processing it.

## Quick start

1. Install the [.NET 10 Desktop Runtime for Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0) if needed.
2. Download the release ZIP, extract it completely, and run `HopeChestCore.exe`.
3. Enable replay recording in Fortnite under **Settings → Game → Replays**.
4. HopeChestCore imports completed replays automatically. The default folder is `%LOCALAPPDATA%\FortniteGame\Saved\Demos`. Use **Replay folder** in the app to select a different folder.

Minimizing sends HopeChestCore to the Windows notification area, where it continues running. Double-click its icon to reopen it. Closing the window exits the app.

## How it works

1. HopeChestCore checks the replay folder every 12 seconds and waits for recordings to finish.
2. The local replay parser counts chests you opened or destroyed and team chests recorded in the replay. It excludes ammo boxes.
3. A Fortnite session ID and a SHA-256 hash of the replay file identify duplicate imports. Copies of the same recording count as one match. Version 0.4.0 also merged entries that shared a session ID when upgrading an existing database.

The dashboard shows your chests, team chests, the combined total, and the number of tracked matches. The comparison chart plots your chests and the total for the last 20 matches. You can switch between chests per match and per minute. The chart shows both averages.

**Drop odds** estimates the chance of finding the Weekly Golden Outfit (1 in 1,562,500 per chest) or Pirate Shop Access (1 in 137,500) at least once. It uses the dashboard's **total chests** in the selected period and assumes a fixed, independent chance for each counted chest. The model excludes event boosts and loot availability windows.

The period filter applies to the dashboard and match history. View all days, today, or a date in your history. The app uses the local date when the match started.

Per-minute rates use replay duration. Matches with no recorded duration still count towards totals and per-match averages. HopeChestCore can fill in the duration if the replay is still available.

**Reset statistics** clears saved matches and import records from the app's local database. Counting resumes with matches that start after the reset.

## Disclaimer

HopeChestCore is an unofficial fan tool with no affiliation with Epic Games, Inc. Fortnite and related marks, as well as copyrighted Epic material, belong to Epic Games, Inc. Epic retains all rights to that material. Epic has neither endorsed nor approved this project. Use the app at your own risk. [Epic Games Fan Content Policy](https://legal.epicgames.com/epicgames/fan-art-policy)

Statistics depend on which events Fortnite recorded. Events outside your vicinity can be missing, especially for teammates. Changes to the replay format may affect analysis and require a parser update.

## Build from source

Install the .NET 10 SDK and run these commands in the project directory.

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false
```

## Licenses and credits

Replay analysis is based on Fortnite-ChestCounter 1.5.1 (LED-N, MIT, commit `1ec3636`) and its fork of FortniteReplayDecompressor (Shiqan, MIT). The MIT license notices are in [`vendor/`](vendor/).

The bundled OozSharp decompressor includes code by Powzix under GPL-3.0-or-later. The combined HopeChestCore distribution is provided under GPL-3.0-or-later. See the [GPL license text](vendor/LICENSE-OozSharp-GPL-3.0).
