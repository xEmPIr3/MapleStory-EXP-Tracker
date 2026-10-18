# Building MapleStory Classic EXP Tracker

*Created by Aydrian.* The source code is published for reading and for building your own personal copy only;
see [LICENSE](LICENSE). All rights are reserved.

## Requirements

Windows 10 or 11. Nothing to install: `build.bat` compiles everything with the C# compiler that ships with
Windows (.NET Framework 4.x).

## Build

```
build.bat               builds MapleExpTracker.exe
build.bat test          builds, then runs the tests
build.bat docs [dir]    builds, then draws the README images into dir (default: docs)
```

The tests cover the parser, the tracker maths, the EXP table, reading the game's font from real captures at
game scales from 1x to 4x, the mouse cursor or a window covering the numbers, and Windows OCR on sample EXP bars.

## How the code is organized

| File | What it does |
|---|---|
| `src/Program.cs` | Entry point, single-instance check, error log |
| `src/OverlayForm.cs` | The always-on-top tracker window, settings menu and tray icon |
| `src/OverlayRenderer.cs` | Draws the tracker, and the 12 color themes |
| `src/OcrWorker.cs` | Background thread that reads the EXP region once a second |
| `src/OcrService.cs` | Screen capture, image clean-up and the Windows OCR fallback |
| `src/PixelFontReader.cs` | Reads MapleStory's 5x7 pixel font by shape, at any game scale |
| `src/ExpParser.cs` | Turns the text that was read into EXP and percentage |
| `src/ExpTable.cs` | EXP needed for levels 1–99 (data from meowdb.com) |
| `src/ExpTracker.cs` | Level detection, level ups, deaths, EXP/h, session totals and time to level |
| `src/RegionSelectForm.cs` | The screen region picker with a magnifier |
| `src/OcrTestForm.cs` | The Test OCR window |
| `src/Settings.cs`, `src/TrackerLog.cs` | `settings.ini` and `tracker.log` |
| `tests/Tests.cs` | The test suite (`tests/fixtures` holds real captures of the game's status bar) |
| `tools/DocImages.cs` | Draws the README images with the app's own code |

## Files the tracker writes

- `settings.ini` next to the exe: region, level, name, position, size, opacity, theme. `interval_ms` sets how
  often the EXP bar is read (default 1000).
- `tracker.log`: level changes, re-syncs and ignored readings, with the exact text that was read.
- `error.log`: only written if something goes wrong.

## Releasing a new version

1. Bump the version in `src/AssemblyInfo.cs` and run `build.bat test`.
2. If the look changed, run `build.bat docs images\vX.Y.Z` and point the picture links in `README.md` at the
   new folder (a new folder name makes browsers load the new pictures right away).
3. Commit, push, and attach the exe to a release:
   `gh release create vX.Y.Z MapleExpTracker.exe --repo xEmPIr3/MapleStory-EXP-Tracker`
