# MapleStory Classic EXP Tracker

*Created by Aydrian*

> Private source repository. The public download page is
> [xEmPIr3/MapleStory-EXP-Tracker](https://github.com/xEmPIr3/MapleStory-EXP-Tracker).

A small always-on-top overlay that watches your EXP bar and shows:

| | |
|---|---|
| **LV + EXP bar** | Your level, current EXP / total EXP the level needs, and the percentage, updated about once a second |
| **LAST 5 MIN** | EXP gained in the last 5 minutes |
| **EXP/H - 30M** | EXP per hour over the last 30 minutes (or the whole session if shorter) |
| **EXP/H - 1H** | EXP per hour over the last hour (or the whole session if shorter) |
| **THIS SESSION** | EXP gained since you started (or since *Reset Session*) |
| **SESSION TIME** | How long the session has been running (stops while paused) |
| **TO LV. N** | Time to the next level at your 30-minute EXP/h |

It reads the EXP numbers off your screen. MapleStory Classic's own number font is recognized directly by
shape, which is exact. Anything else falls back to the OCR engine built into Windows. It never touches the
game's memory or files, so there's nothing for anti-cheat to object to.

## Getting started

1. Run **`MapleExpTracker.exe`**. The tracker appears in the top-left corner and an icon appears in the system tray.
2. Start the game and get your EXP bar on screen. Play in **windowed** or **borderless** mode
   (exclusive fullscreen can't be captured).
3. Click the **gear** at the top right of the tracker → **Select EXP Region…**, then drag a box around the EXP numbers,
   e.g. `613128 [62.73%]`. Include the whole number and the percentage with a little space around them.
   A magnifier helps you line it up.
4. Enter your current level (and optionally a name for the header).

That's it. The dot in the header turns green while EXP is being read.

## Controls

- **Drag** the tracker to move it (it remembers its position).
- **Gear button** (top right) opens the settings menu: select region, test OCR, set level/name, reset session,
  pause, size, opacity, theme, click-through, hide, exit. Right-clicking the tracker opens the same menu.
- **Themes** (gear → Theme): Default (Gold), Pink, Red, Black, Orange, Blue, Purple, Green, Teal, Yellow,
  Lavender and Rainbow. All are dark; the choice is remembered.
- **Double-click** the level badge or the name to change them.
- **Tray icon**: same menu; double-click to show/hide the tracker. Use it to turn *Click-Through* off again.

## How it works

- **EXP table:** the EXP needed for levels 1–99 comes from the
  [meowdb MapleStory Classic EXP table](https://meowdb.com/msclassic/guides/exp-table-level-1-to-100)
  (in `src/ExpTable.cs`, checked against the game: 194,111 EXP at level 41 shows as 59.87% of 324,240).
- **Your level is detected from your EXP.** Number ÷ percentage has to match exactly one level in the
  table, so the level fixes itself if it was set wrong.
- **Misreads are ignored.** A reading that doesn't fit your level is thrown away. That includes the mouse
  cursor or a window covering part of the numbers.
- **Level ups** count only when your EXP resets and the new numbers fit the next level's EXP exactly. The
  EXP that finished the old level plus the overflow into the new one is added to the session.
- If the game's numbers ever stop matching the table (e.g. after a patch), the tracker falls back to working
  out the level size from the percentage alone.
- **Deaths** (EXP loss) are subtracted from the session.
- Odd readings are only accepted after being seen twice in a row, so a single OCR glitch can't spike the numbers.
- **Pause** stops the clock too, so breaks don't lower your EXP/h. EXP gained while paused isn't counted.
- If the game shows EXP as `current / total` instead of a percentage, that works too.

## If it can't read your EXP

Gear → **Test OCR…** shows exactly what was captured, what the OCR read and what the tracker made of it.

- Make sure the box covers only the EXP numbers (not the HP/MP numbers), with a few pixels of margin.
- Don't put the tracker itself on top of the EXP numbers.
- If the game window is scaled down a lot, the text can get too small or blurry to read. A larger window helps.

## Files

- `settings.ini` is created next to the exe (region, level, position, size, theme…). `interval_ms` sets how
  often the EXP bar is read (default 1000).
- `tracker.log` notes level changes, re-syncs and ignored readings, with the exact text that was read.
  If the tracker ever does something odd, this file shows why.
- `error.log` is written only if something goes wrong.

## Building from source

No installs needed: `build.bat` compiles everything with the C# compiler that ships with Windows (.NET Framework 4.x).

```
build.bat               builds MapleExpTracker.exe
build.bat test          builds, then runs the tests (parser, tracker maths, and Windows OCR on sample EXP bars)
build.bat docs [dir]    builds, then draws the download-page images (tools/DocImages.cs)
```

### Publishing a new version

1. Bump the version in `src/AssemblyInfo.cs` and run `build.bat test`.
2. If the look changed, run `build.bat docs public\images\vX.Y.Z`, point the picture links in
   `public\README.md` at the new folder and delete the old one. (A new folder name makes browsers load the new
   pictures; GitHub lets them keep the old ones for a while otherwise.) Then commit and push the `public` folder
   (it's its own repository: the public download page).
3. Attach the new exe to a release on the public repository:
   `gh release create vX.Y.Z MapleExpTracker.exe --repo xEmPIr3/MapleStory-EXP-Tracker`

Source is in `src/`. The interesting parts are `ExpParser.cs` (turning OCR text into numbers), `ExpTracker.cs`
(level-ups, deaths, rates) and `OcrService.cs` (capture, image clean-up and Windows OCR).
