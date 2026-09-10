# Roadmap

Feature ideas under consideration for future releases. Nothing here is scheduled or spec'd in
detail — this is a place to keep track of them so they don't get lost, not a commitment.

## Auto-detect party size at run start

Read the actual number of players in the party when a run starts and set the Solo/Duo/Trio/Quad
selector automatically, instead of requiring it to be set by hand before diving. Likely reads off
the same player-array structure `AbyssusStatsReader` already walks to reach the local player's
own state — the array's element count, not just its first entry.

## Saveable overlay/settings profiles

Let a user save named profiles (e.g. "Streaming," "Practice," "Race") that each remember their
own overlay configuration and other settings, and switch between them from the Configurator
instead of only ever having one global settings file. Biggest design question: how much of
`AppSettings` belongs to a profile versus stays global (window position, update-check
preference, etc. probably shouldn't be per-profile).

## Per-weapon PB/run tracking for All Weapon%

"All Weapon%" (`RunCategory.AllWeaponPercent`) already exists as a category and already uses the
same floor-based splits as Any% — it's not a different run structure, just a different goal: an
Any%-equivalent completion done once per weapon in the game, one weapon per attempt. Right now
every attempt in that category shares one PB pool regardless of which weapon was used, which
doesn't make much sense given how differently weapons play. The improvement: detect which weapon
a run started with and track PBs/history separately per weapon within All Weapon%, the same way
Solo/Duo/Trio/Quad already split PBs apart by player count. Needs a memory read for the
player's starting/equipped weapon, not a pickup-event detector.

## Per-Deepwater-level PB/run tracking

Detect which Deepwater level a run is played on and track personal bests and run history
separately per level, the same way category and player count are already split apart — a
Deepwater 3 time shouldn't compete against a Deepwater 1 time as if they were the same category.

## Track abilities, blessings, and charms taken during a run

Record which ability was chosen and which blessings/charms were picked up over the course of a
run. Two uses: auto-detecting a "No Blessings" run for that challenge category without the
runner having to self-report it, and new Stats-page breakdowns (e.g. average time by blessing,
how often a given charm gets taken).
