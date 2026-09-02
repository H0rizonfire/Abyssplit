# Contributing to Abyssplit

Thanks for taking an interest in the project.

Abyssplit works by reading values out of Abyssus's own process memory. Because of that, changes
here can have real consequences — for other runners' leaderboard eligibility and for the
integrity of the timing data itself. Combined with the size of the codebase, that means
**pull requests aren't accepted unsolicited.**

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
