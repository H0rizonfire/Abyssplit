# Changelog

All notable changes to Abyssplit are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.2.0] - 2026-10-14

### Added

- **All Weapon% is now timed as one continuous attempt** — a successful run with each of the nine
  weapons, in any order. Abyssplit detects which weapon you're using and shows a row per weapon
  with that run's own time, its usual biome/depth/room splits underneath, the time between runs
  (post-kill and lobby, with loading removed), and the attempt's running total on the overlay. A
  death, quit, reset, or closing the game ends the attempt; repeating a weapon doesn't (its time
  counts, but not toward the nine). History keeps one entry per attempt, with the PB as the best
  completed total and a best time per weapon. Two new settings (both off by default): show all
  nine weapons as a checklist from the start, and also save each weapon run on its own (as an Any%
  run, or a True Ending run in All Weapon% True Ending).
  All Weapon% times recorded by earlier versions were single runs; they're kept, separately from
  the new attempts.
- **All Bosses: which biome bosses are still needed** — in the All Bosses category, the overlay
  and Main tab show how many of the four biome bosses (The Golemancer, Captain Gwaelod, General
  Kri'su, Highpriest Un'glu) you've defeated this run and which are still needed, then turn
  green once all four are down so you know it's time for the Royal Abyss. A boss counts once you
  leave its depth. Mini bosses and the Royal Heralds aren't tracked.
- **PB Progression chart shows your recent runs** — the Stats tab chart now plots your most
  recent completed runs (10, 25, 50 or 100; 25 by default), with the runs that set a new PB
  in green, instead of only your PBs. It appears once you have two completed runs.
- **All Weapon%: Scroll keeps the weapon you're playing in view** — with List Overflow Behavior
  set to Scroll and the checklist on, the overlay scrolls to the weapon being played rather than
  the bottom of the list.
- **"Joined mid-run" notice** — if Abyssplit is opened (or the game attaches) while a run is
  already in progress, that run can't be timed, and only the IGT card moves. Abyssplit now says
  so on the overlay and the Main tab, instead of leaving it looking like the timer is stuck. Your
  next run from the lobby is timed normally.
- **Weapon and Status styling** — Section Styling (Overlay tab) has two new sections, each with
  its own font size and color: **Weapon**, for All Weapon%'s weapon rows and its "Between runs"
  row, and **Status**, for the lines under the timer (Prev. Run, the All Bosses progress, the
  weapon count, the "Joined mid-run" notice). Preview now shows sample weapon rows and status
  lines too, and the overlay's title reads "Lobby" between runs.
- **All Weapon% True Ending** — a new category for the True Ending version of All Weapon%, with
  its own personal best and history. Every weapon run must get the True Ending: a run that ends
  with the normal ending still counts its time but not toward the nine, and its row says
  *normal ending, not counted* so you know straight away.
- **Everything a run submission needs, in one place** — a run's History Details now show every
  time (Load+Cutscene-Free, Load-Free and IGT), the weapon used, and the game's version and store
  build. **Copy** puts all of that in its text (it used to give one time), and **Share**'s file
  includes the weapon and game version too. Runs recorded before this version don't have a weapon
  or game version saved.
- **Your own PB sound** — Settings has a new **PB Sound** row under Play Sound on New PB: pick any
  WAV, MP3, WMA, M4A or AAC file to play when you set a new personal best, **Test** it, or
  **Clear** it to go back to the Windows default. If the file is later moved or won't play, the
  default sound plays instead. A **PB Sound Volume** slider (50% by default) sets how loud it
  plays — your own file and the Windows default alike.
- **Clearer first launch** — before the game is running, the Main tab says Abyssplit is waiting
  for Abyssus, that it finds the game on its own (Steam or PC Game Pass), and that timing starts
  by itself when you begin a run. An empty splits list now says your splits will appear once a
  run starts.
- **Session recordings** — Abyssplit now records what it read from the game during each session
  (depth, room, timers, loading and cutscene state) to `%APPDATA%\AbyssusTimer\recordings\`, so a
  reported bug can be replayed exactly. Recordings hold no personal information, never leave your
  PC unless you attach one to a bug report, and clean themselves up (14 days, 200 MB max).
  **Report a Bug** names the current session's recording.

### Fixed

- **Report a Bug's log excerpt was always empty** — it showed "(could not read log file)"
  because the log is kept open while Abyssplit runs. The excerpt now includes the end of
  today's log as intended.
- **A finished run could be saved as a 0:00 personal best** — occasionally the game resets its
  run clock while it still reports being in a run. Abyssplit took that for a new run starting,
  so the finished run was recorded as 0:00 (or, for a quit, lost its time), sometimes with a
  negative split time. Abyssplit now keeps the run's last real time and splits until the run
  ends. Impossible personal bests and gold splits (0:00 or less) left by this bug are removed
  automatically the next time Abyssplit starts, and your personal best is restored from your
  fastest real completed run in History. Thanks to CobbyAyy for the report and log.
- **Load-Free and Load+Cutscene-Free times were slightly too high** — whenever the game's own
  clock briefly hadn't updated between two of Abyssplit's checks (a low frame rate or a hitch),
  that moment could be counted twice, adding a fraction of a percent to a run (about 6 seconds on
  a 24-minute test run). Both timers now follow the game's own clock exactly; with "Pause Stops
  Time" off, time spent paused still counts, as before. The in-game time (IGT) display was never
  affected. **Because earlier times were slightly inflated, runs from this version on will
  compare a little favorably against personal bests set on older versions.**
- **False "possible game update" warning at startup** — opening Abyssplit before the game
  finished loading could show the warning even though nothing was wrong. The check now waits
  until the game reaches the lobby.
- **Empty 0-second attempts in History** — relaunching the game (or switching between Steam and
  Game Pass) while Abyssplit stayed open could record a phantom attempt while the game was still
  loading. Abyssplit now ignores everything until the game reaches the lobby after each launch.
- **Log flooding when the game closes** — closing the game made Abyssplit attach and detach
  dozens of times in a second, filling the log that Report a Bug attaches.
- **Lost runs after a run ended oddly** — if the game reset its run stats just before a run ended,
  Abyssplit could get stuck repeatedly ending that run, and that attempt and every later one in
  the session were never recorded until Abyssplit was restarted. The attempt is now recorded
  normally and later runs are unaffected.
- **History rows hid each run's time** — every row in the History tab was cut off before the run's
  time, and so was the Specific Run picker in Settings. Each History row now shows the date with
  "Completed — 22:18.00" (or "Incomplete") under it, and the picker lists the time first.
- **Everything in the main window's tabs was bold** — the selected tab's bold style spilled into
  the whole tab, so headings and the weapon and biome rows didn't stand out from the text around
  them. Only the tab's own name is bold now.
- **Times over an hour showed as minutes** — a multi-hour All Weapon% attempt read "212:12.00";
  times of an hour or more now show hours, "3:32:12.00". Shorter times look the same as before.
- **Software Rendering's description was cut off** — it now wraps like the other settings'
  descriptions.
- **Main window consistency** — empty sections now say so ("No runs recorded yet for this
  category.", "Select a run above to see its splits.", "No PB recorded yet for this category.")
  instead of showing a heading with nothing under it; All Weapon%'s "Between runs" row on the Main
  tab is styled like the weapon rows, as it is on the overlay; split deltas are the same size as
  the rest of their row; and a few buttons, pickers and notes now match the size of the others
  like them.

## [0.1.2] - 2026-10-11

### Added

- **PC Game Pass support** — Abyssplit now works with the Game Pass version of Abyssus as well
  as Steam, and detects which one is running automatically. Previously it only recognized the
  Steam version and never attached to Game Pass. Steam behavior is unchanged.
- **Game version in bug reports** — **Report a Bug** now includes which version of the game you're
  playing (Steam or Game Pass, with its build number when available), and the log records it each
  time Abyssplit attaches. The GitHub bug report form also asks for it.

## [0.1.1] - 2026-09-09

### Added

- **Overlay visibility** — the overlay now stays hidden until there's a reason to show it:
  while attached to Abyssus, while the Configurator window is visible, or always, if "Always
  Show Overlay" is turned on in Settings. Previously it appeared as soon as the app launched,
  which meant it could sit on screen unexpectedly when Abyssplit starts automatically at sign-in.
- **Reset Run hotkey** — a global `Ctrl+Alt+R` hotkey triggers the same Reset Run action as the
  button on the Main tab, so it works even when the Configurator is closed or minimized. Off by
  default — enable it in Settings.
- **Check for Updates** — a button on the Settings tab checks GitHub for a newer release on
  demand, rather than only ever finding out at the next launch.
- **Crash resilience** — if the app or game closes unexpectedly mid-run, the in-progress attempt
  is recovered as an incomplete entry in Run History the next time Abyssplit starts, instead of
  being lost entirely.
- **PB sound cue** — a short sound plays whenever a full-run personal best is beaten. Can be
  turned off in Settings ("Play Sound on New PB").
- **Copy splits as text** — a "Copy" action on any History tab entry copies that attempt's
  splits as plain text, for quick sharing without exporting a file.
- **PB Progression chart** — the Stats tab's PB history now includes a small chart with scale
  labels and a marker at each personal-best milestone (hover one for its date and time), instead
  of just a plain trend line.
- A [Troubleshooting](TROUBLESHOOTING.md) doc covering the most common issues — the overlay not
  showing, the SmartScreen warning, attach failures, and Run at Startup — in one place instead of
  spread across other docs.
- A warning banner if Abyssus appears to have updated in a way that might affect timing accuracy
  (a game patch can shift what Abyssplit reads internally). Informational only — it never blocks
  splits, PBs, or history from recording, so a false alarm can't cost you a real time.

### Changed

- Clarified in the User Guide that "Run at Startup" works for both the installed version and the
  portable exe, but points at wherever the exe was running from at the time — moving, renaming,
  or replacing a portable exe afterward silently breaks it until re-enabled from the new location.
- Documented that Abyssus needs to run in Borderless or Windowed mode for the overlay to be
  visible — exclusive Fullscreen bypasses Windows' compositor, hiding every overlay (Abyssplit
  included) behind the game.

### Fixed

- Settings, personal bests, run history, and crash-recovery files are now saved atomically. A
  crash or power loss partway through a save could previously corrupt the existing file with
  nothing to fall back to; the old file is now only ever replaced once the new one has fully
  written successfully.

## [0.1.0] - 2026-09-02

Initial release.

### Added

- Live timer with three views side by side — in-game time, Load-Free, and Load+Cutscene-Free
  (the one splits and personal bests are actually built on) — read directly from Abyssus's own
  memory, no manual splitting required.
- Automatic floor-by-floor and room-by-room splits with a running delta against a previous run,
  a personal best, or a specific past attempt.
- Full run history — every attempt, finished or not — plus a Stats tab with per-location
  averages, a consistency figure, and PB progression over time.
- Export any attempt as a `.abysplit` file to share with a friend or attach to a run submission.
- A fully customizable overlay: opacity, scale, an optional background image, independent font
  size/color per row, and a choice of how much split detail to show.
- Tray minimization, launch-at-startup, and a click-through overlay by default.
- An installer and a portable exe, both built from the same source.

[Unreleased]: https://github.com/H0rizonfire/Abyssplit/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/H0rizonfire/Abyssplit/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/H0rizonfire/Abyssplit/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/H0rizonfire/Abyssplit/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/H0rizonfire/Abyssplit/releases/tag/v0.1.0
