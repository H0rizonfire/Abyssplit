# Changelog

All notable changes to Abyssplit are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

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

[Unreleased]: https://github.com/H0rizonfire/Abyssplit/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/H0rizonfire/Abyssplit/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/H0rizonfire/Abyssplit/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/H0rizonfire/Abyssplit/releases/tag/v0.1.0
