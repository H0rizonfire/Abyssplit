# Abyssplit

**[Visit the website →](https://h0rizonfire.github.io/Abyssplit/)**

A speedrun timer and overlay for [Abyssus](https://store.steampowered.com/), reading run state
directly out of the game's own memory — no manual splitting required. It sits on top of the game
as a separate window: a small always-on-top overlay for live timing, and a configurator window for
history, stats, and settings.

**Status:** early — versioning starts at `0.1.0`. Not in official use.

**Not affiliated with Abyssus's developer or publisher.** Using any third-party tool with an
online game is done at your own risk — see [Terms of Use](#terms-of-use--license) below, and check
the rules of any leaderboard or event before submitting a time.

For a full walkthrough of every tab and setting, see the [User Guide](USER_GUIDE.md).

## Features

### Timing

Three timers run side by side, each answering a different question about a run:

- **IGT** — the game's own raw, unmodified run-time stat.
- **Load-Free** — IGT with loading-screen time subtracted out.
- **Load+Cutscene-Free** — Load-Free with cutscene time subtracted too. This is the number splits
  and personal bests are built on, so a run's recorded time isn't inflated by a boss's pre-fight
  cinematic.

A **Pause Stops Time** setting controls whether pausing the game also pauses these timers — off by
default, since most leaderboards don't allow pausing. Enabling it can make a run ineligible unless
the specific ruleset you're submitting to explicitly allows pausing.

A couple of other session-behavior settings round this out: **Auto-Reset on Lobby Return** clears
the split list as soon as you're back in the lobby, ready for the next attempt, and **Software
Rendering** trades a little CPU cost to avoid the overlay stuttering if it's competing with the
game for GPU time.

### Splits

Every run is broken down floor-by-floor and room-by-room automatically as you play, with:

- A running **Personal Best** comparison per floor, and a **NEW PB** flag the moment one is beaten.
- A **+/-  delta** against whichever comparison you've selected (see below), shown live while a
  segment is still in progress.
- Automatic biome and boss naming, and correct handling of multi-part encounters (Royal Abyss's
  Heralds, To'raka's two phases) as a single continuous split rather than one row per game-internal
  depth.

**Split Comparison** (Settings tab) lets you choose what the delta is measured against:
- **Best** — your all-time best segment for each floor.
- **Previous** — your immediately preceding attempt.
- **Specific Run** — any past attempt from your history.
- **Imported File** — a split file someone else shared with you (see below).

### Run History & Stats

- **History tab** — every attempt (finished or not), with its final time and a **Details** view of
  the full per-floor breakdown. Auto-deletion of old attempts is optional and configurable (off by
  default).
- **Stats tab** — per-location average/median/best/worst segment times with a consistency figure,
  your PB's progression over time, and how far you've reached across all attempts.
- **Categories & player count** — Any%, All Bosses, True Ending, and their Glitchless variants,
  each crossed with Solo/Duo/Trio/Quad, all tracked as fully independent PB pools and history.

### Sharing splits

From the History tab, click **Share** on any attempt to export it as an `.abysplit` file — its
IGT, Load-Free, and Load+Cutscene-Free times and full per-floor splits all included, alongside a
runner name you'll be asked for first, so whoever races it knows whose time they're up against.
Send that file to another runner, or attach it when submitting a run for record verification. To
race against a file someone sent you, use **Import Split File** on the Settings tab, then set
**Split Comparison** to **Imported File**.

### Overlay

The in-game overlay is a borderless, click-through, always-on-top window, fully customizable from
the Configurator's **Overlay** tab:

> **Requires Abyssus to be in Borderless or Windowed mode.** Exclusive Fullscreen bypasses
> Windows' compositor, which hides every overlay behind the game — not a bug specific to
> Abyssplit, the same applies to Discord/Steam overlays. Switch Abyssus's Window Mode setting if
> the overlay isn't appearing.

- **Appearance** — background opacity, overall scale, and an optional custom background image.
- **Section styling** — independent font size and color for the Biome/Depth/Floor rows, and a
  toggle for whether times get their own background chip.
- **Split display** — how much detail shows (nothing, one row per floor, or an expanded room-by-
  room view for whichever floor is currently in progress) and how a long list behaves (collapse
  completed sections, scroll, or just grow).

Toggle **Edit Overlay Layout** from the Configurator to drag/resize the overlay; it's click-through
and non-interactive the rest of the time so it never gets in the way in-game. The overlay's title
line always shows the running app version, so it's identifiable from a screenshot or stream clip
without needing the exported split file for that.

### Staying up to date

Abyssplit checks GitHub for a newer release once at startup and shows a dismissible banner in the
Configurator if one's available. Times recorded on an outdated version may not be accepted for
leaderboard submission, so it's worth keeping current. This check is best-effort — no network
access, or GitHub being unreachable, just means it silently doesn't show anything.

### Running in the background

- **Minimize to tray** — minimizing the window hides it to a system tray icon instead of the
  taskbar. Right-click the tray icon for **Show Abyssplit**, **Reset Run**, and **Exit**.
  Double-click to restore.
- **Run at Startup** (Settings tab) — launches Abyssplit straight to the tray when you log in.

## Installation

Two ways to get Abyssplit, both from the [Releases](https://github.com/H0rizonfire/Abyssplit/releases)
page once one is published:

- **Installer** — installs to your user profile (no admin rights needed), adds a Start Menu entry,
  and optionally a desktop shortcut. Recommended for most people.
- **Portable** — a single self-contained `.exe`, no installation. Good for USB drives or if you'd
  rather not install anything.

On first launch, you'll be asked to accept the app's Terms of Use (see below) — this only happens
once per version.

**Windows may show a SmartScreen warning** ("Windows protected your PC") the first time you run
either download — this is expected for any app that isn't signed with a paid code-signing
certificate, not a sign anything is wrong. Click **More info**, then **Run anyway** to proceed.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
dotnet build src/AbyssusTimer.sln -c Release
```

Configurations: `Debug` (local dev, default), `Release` (public build), `Trusted` (adds verbose
diagnostic logging for troubleshooting — never shipped publicly).

## Data storage

Everything Abyssplit saves lives under `%APPDATA%\AbyssusTimer\` — settings, personal bests, run
history, and a rolling log file per day. Nothing is transmitted anywhere automatically; the only
network-adjacent actions are ones you trigger yourself (opening a pre-filled GitHub issue, sharing
an exported split file).

## Terms of Use & License

Abyssplit reads timing-relevant memory from the running game process and never writes to it. Full
terms are shown on first launch and are always available from the About panel (Settings tab).

Licensed under [PolyForm Noncommercial 1.0.0](LICENSE.md) — free to use, share, and modify for any
noncommercial purpose; commercial use isn't permitted.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) — please open an issue before starting work on a change.

After cloning, enable the repo's pre-commit safety hook (blocks accidental large files and
secret-shaped strings before they're committed):

```
git config core.hooksPath .githooks
```

## Project layout

- `src/AbyssusTimer.App/` — the WPF app (timer UI, overlay, settings).
- `src/AbyssusOverlay.Core/` — shared game-memory-reading logic (process attach, state tracking).
- `installer/` — the Inno Setup script used to build the installer.

## Reporting issues

Use the app's built-in **Report an Issue** button (Settings tab) — it opens a pre-filled GitHub
issue with a recent log excerpt and reveals the full log file for you to attach if needed. You can
also [open an issue directly](https://github.com/H0rizonfire/Abyssplit/issues).

## Support

Abyssplit is free, with no strings attached — use it, keep it, share it. If you'd like to support
this project and whatever comes after it, you're welcome to
[buy me a coffee](https://buymeacoffee.com/h0rizonfire), but it's entirely optional and never
expected.
