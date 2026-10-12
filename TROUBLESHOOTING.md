# Troubleshooting

Common issues and their fixes, in one place. If none of these match what you're seeing, use the
app's own **Report a Bug** button (Settings tab) — it attaches your version, which version of the
game you're on (Steam or Game Pass), and a log excerpt automatically, which is usually the fastest
way to get help.

## The overlay isn't showing up

Abyssus needs to be running in **Borderless** or **Windowed** mode, not exclusive Fullscreen.
Fullscreen mode bypasses Windows' own compositor, which hides *every* overlay — Discord and Steam
overlays included, not just this one — behind the game instead of on top of it. Switch Abyssus's
Window Mode setting and the overlay should appear immediately.

If you're already in Borderless/Windowed and it's still not showing, try turning on **Always
Show Overlay** (Settings tab) — that forces the overlay to display regardless of game state, which
helps tell an attach problem apart from a rendering one.

## Windows shows a security warning when I try to run it

Expected — Abyssplit isn't signed with a paid code-signing certificate, which is what triggers
SmartScreen's "Windows protected your PC" warning for any small, free tool like this one. Click
**More info**, then **Run anyway**. It isn't a sign anything is wrong with the download.

## Abyssplit won't attach ("Waiting for Abyssus" never changes)

- Make sure Abyssus is actually running — Abyssplit attaches to a live game process, it doesn't
  launch the game for you.
- **On PC Game Pass?** Older versions of Abyssplit only recognize the Steam version of the game,
  so make sure you're on the latest release (Settings tab → **Check for Updates**).
- **Run both at the same elevation level.** If Abyssus is running as administrator and Abyssplit
  isn't (or vice versa), Windows blocks the non-elevated process from reading the elevated one's
  memory, and Abyssplit will never attach. Either run both elevated or run neither elevated.
- If the game recently updated, an attach failure — or splits/times that look obviously wrong —
  can mean the update shifted something Abyssplit reads internally. Worth a bug report either
  way, see below.

## Run at Startup doesn't seem to be working

Works for both the installer and the portable exe, but it points at wherever the exe was running
from **at the time you enabled it**. If you're on the portable exe and later moved, renamed, or
replaced that file, the entry now points at a file that's no longer there — turn the setting off
and back on from the new location.

## Splits or times look wrong

Abyssplit reads Abyssus's internal state directly, keyed to specific values that can shift when
the game updates. If a split fires at the wrong moment, a time looks impossible, or a floor gets
misidentified, please report it (see below) — include which floor/depth you were on and roughly
when in the run it happened, that's usually enough to track down.

## Where is my data stored?

Everything Abyssplit saves — settings, personal bests, run history, and daily logs — lives under
`%APPDATA%\AbyssusTimer\`. Nothing is sent anywhere automatically; the only network-adjacent
actions are ones you trigger yourself (checking for updates, opening a pre-filled GitHub issue,
sharing an exported split file).

## Still stuck?

Use **Report a Bug** (Settings tab) — it's pre-filled with your app version and a recent log
excerpt, and reveals the full log file in case more detail is needed. You can also
[open an issue directly](https://github.com/H0rizonfire/Abyssplit/issues).
