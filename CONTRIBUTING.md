# Contributing to Abyssplit

Thanks for taking an interest in the project.

Abyssplit works by reading values out of Abyssus's own process memory. Because of that, changes
here can have real consequences — for other runners' leaderboard eligibility and for the
integrity of the timing data itself. Combined with the size of the codebase, that means
**pull requests aren't accepted unsolicited.**

## Timer integrity

Abyssplit is the source of truth for competitive speedrun times. Any change that alters timer
behavior — timing arithmetic, split detection, load/cutscene exclusion, pause handling, PB or
comparison logic — invalidates the meaning of every time already recorded against it, not just
runs going forward. Treat this as categorically more sensitive than any other part of the app.

**Never let timer behavior change as a side effect of something else** (a refactor, an unrelated
feature, a "harmless" cleanup). If a change is genuinely meant to change timer behavior, that must
be the explicit, stated purpose of the PR — not something that happens to fall out of it.

`tests/AbyssusTimer.Tests` (not part of `src/AbyssusTimer.sln` — run it directly with
`dotnet test tests/AbyssusTimer.Tests/AbyssusTimer.Tests.csproj`) drives the real `TimerEngine`
with scripted game data specifically to catch this kind of drift. Run the full suite after any
change touching `TimerEngine.cs`, `AdjustedTimer.cs`, `RunSession.cs`, `AbyssusStatsReader.cs`, or
`GameOffsets.cs` — every time, not just when the build succeeds. `TimerIntegrityTests.cs`
specifically locks in the timer-arithmetic edge cases that took multiple wrong attempts to get
right historically (the AdjustedTimer start-gate, Pause Stops Time, cutscene exclusion, the True
Ending Phase 2 freeze) — if one of those tests ever needs to change, stop and make sure that's
really an intended behavior change before touching it.

**Verifying a new timer test is meaningful never means editing the real timer source, even
temporarily.** To prove a test actually catches the regression it's meant to catch, do it in a
disposable `git worktree` (`git worktree add ../some-scratch-name master`), make the "break" only
there, run the test from that copy, confirm it fails, then delete the whole worktree — never
edit-then-revert the tracked files in the real working copy. A revert that's supposed to restore
byte-for-byte-identical code is still a moment where the shipped source is wrong on disk, resting
entirely on getting the revert right; a disposable copy removes that risk instead of managing it.

## Handling a game update

Abyssus updating can shift the memory offsets `GameOffsets.cs` reads from, and the failure mode
isn't a crash — it's silently *wrong* data (bad floor numbers, bogus times) with no warning,
which is worse for a tool people submit times from. Two independent layers detect this and raise
`TimerEngine.IsPossibleGameMismatch` (a dismissible Configurator banner, warn-only — it never
blocks PB/history recording, since a false positive costing someone a legitimate PB is worse than
asking them to use judgment on a flagged time):

- **Layer 1** (`GameVersionChecker`) — compares the attached exe's own `FileVersionInfo` against
  `GameOffsets.LastVerifiedGameVersion`. **Checked against the real game (2026-09-04) and
  confirmed permanently unusable**: `RGame-Win64-Shipping.exe` reports `ProductVersion`/
  `FileVersion` as `"UE5-CL-0"` on both — Unreal's generic default packaging stamp (the engine
  build, not the game's own version), which won't change across Abyssus updates. Stays `null`
  forever; don't re-investigate or set it speculatively.
- **Layer 2** (`AbyssusStatsValidator`) — plausibility bounds (`GameOffsets.PlausibilityBounds`) on
  the raw values read every tick, independent of any version metadata. Always active, and as a
  result of the above, the sole detection mechanism in practice.

**When a mismatch signal actually fires post-patch** (this will always be Layer 2, since Layer 1
is permanently off — see above):
1. Reproduce locally against the updated game before trusting a single user report.
2. Re-derive the affected offset(s) (existing private process, not documented in this repo).
3. Re-derive `PlausibilityBounds` only if the update changed what's legitimately possible (e.g. a
   new deeper floor) — otherwise leave them.
4. Full rebuild of all three configs + full test suite (see "Timer integrity" above).
5. Manual verify pass: a fresh launch and a real run start-to-finish against the patched game,
   confirming the banner no longer appears (it's sticky per-attach, so this needs a fresh attach
   to actually clear) and floor/room/time/gold all track correctly.
6. Release; call out the offset re-derivation in release notes so runners on the old build know
   to update.

Not planned: a multi-version offset-profile system supporting several game builds simultaneously
— not worth the ongoing maintenance and testing burden once players are expected to be on the
current patch anyway.

## Before writing any code

Open an issue or start a discussion describing what you'd like to change or add, and wait to hear
back before you start. This isn't a formality — it's so we can agree on the approach together
first. A lot of the trickier logic in this codebase (timing edge cases, split detection, run
classification) has already been through multiple wrong attempts before landing on what's there
now. This repo ships without inline comments by design, so working from the code alone — without
that context — is an easy way to reintroduce a bug that was already fixed once. Ask, and I'm happy
to explain how something works, or share a fully-commented copy of the source if you're digging
into a specific area.

If you've found a bug, please just [open an issue](https://github.com/H0rizonfire/Abyssplit/issues)
instead — see the app's own "Report an Issue" button (Settings tab) for the easiest way to include
useful diagnostic info.

## Scope

Contributions that keep to the tool's existing purpose (timing, splits, run history/stats) are
welcome. Anything that would help someone falsify a run or otherwise misrepresent a submitted
time is out of scope and won't be merged.

## Releases

Only `Release`-configuration builds are ever distributed — the installer and the portable exe
(see `src/AbyssusTimer.App/Properties/PublishProfiles/win-x64-portable.pubxml` and
`installer/Abyssplit.iss`, both of which hardcode `Release`). `Debug` and `Trusted` builds
(`Trusted` adds verbose diagnostic logging) are for local development only and are never attached
to a GitHub release.

## License

By contributing, you agree your contribution is licensed under the same terms as the rest of the
project (see `LICENSE.md`).
