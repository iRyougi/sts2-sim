<div align="center">

# sts2-sim

[中文](README.md) | **English**

[![CI](https://img.shields.io/github/actions/workflow/status/iRyougi/sts2-sim/ci.yml?branch=main&label=CI&logo=githubactions&logoColor=white)](https://github.com/iRyougi/sts2-sim/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/iRyougi/sts2-sim?label=release&color=blue)](https://github.com/iRyougi/sts2-sim/releases/latest)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![STS2 v0.111.0](https://img.shields.io/badge/game-STS2%20v0.111.0-8B0000)](#)
[![License: MIT](https://img.shields.io/github/license/iRyougi/sts2-sim?color=green)](LICENSE)
[![保真差异](https://img.shields.io/github/issues/iRyougi/sts2-sim/%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82?label=open%20fidelity%20issues&color=orange)](https://github.com/iRyougi/sts2-sim/issues?q=is%3Aissue+is%3Aopen+label%3A%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82)
[![Stars](https://img.shields.io/github/stars/iRyougi/sts2-sim?style=flat&logo=github)](https://github.com/iRyougi/sts2-sim/stargazers)
[![QQ 1106541324](https://img.shields.io/badge/QQ%20group-1106541324-12B7F5?logo=tencentqq&logoColor=white)](https://qm.qq.com/q/f7BKSECnzU)

**Developer chat (QQ group): [1106541324](https://qm.qq.com/q/f7BKSECnzU)** — questions about usage, porting and fidelity are all welcome.

</div>

An unofficial, headless simulator for **Slay the Spire 2**, written in C# (.NET 9).

This project is not affiliated with or endorsed by Mega Crit. It contains no game files, assets or localization text. Building and testing it does not require the game; comparing its behaviour against the real game requires your own legitimate copy.

## What it is

- **A port of the game logic, not a re-design.** Game rules are ported from the game's own code as literally as practical, so behaviour can be checked method by method. Doc comments cite the original type they come from (for example `MegaCrit.Sts2.Core.Commands.CardCmd`).
- **Seed-exact RNG.** The random number generator and seed derivation follow the game, with the goal that the same seed and the same choices reproduce the same run as the game client, draw for draw.
- **Cloneable.** Combat state can be cloned (`CombatState.Clone()`) for search; RNG streams (`Rng`, `RunRngSet`, `PlayerRngSet`) can be copied exactly (`CloneExact`) or with fresh randomness (`CloneReseeded`) for sampling. Deep cloning of a whole run (`RunState`) is not supported yet.
- **Full runs.** Map generation, Neow and other ancients, combat, elites and bosses, events, shops, rest sites, treasure, rewards, potions, relics, enchantments, and ascension levels 0–10. All cards and potions of game v0.111.0 and all encounters and events of the four acts are ported; a few items are not implemented yet (see below).

| | |
|---|---|
| Game baseline | STS2 v0.111.0 (release commit `41cef1ea`, `main_assembly_hash 222455745`) |
| Characters | Defect, Ironclad, Necrobinder, Regent, Silent |
| Acts | Overgrowth, Underdocks, Hive, Glory |
| Multiplayer | Not supported |

## Fidelity status — please read

The sequential RNG mode **aims** for bit-for-bit agreement with the game. It has been validated by replaying recorded games from the real client and a headless game host and diffing the results, and those comparisons are how most bugs here were found. **Known discrepancies remain and not all of them are fixed**; see issues labelled [`保真差异`](https://github.com/iRyougi/sts2-sim/issues?q=is%3Aissue+label%3A%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82) (fidelity difference). Do not assume a result is identical to the game without checking; if you find a difference, please report it (see below).

There are two RNG modes, and they are deliberately separate APIs:

- **Sequential** (default): matches the game. Use this for anything that must agree with the client.
- **Keyed** (`RunState.CreateKeyedForLabels`): an intentional deviation. Random draws are keyed by purpose instead of drawn in sequence, so two branches of a run stay comparable. Use it only for paired comparisons and training-data generation; it does **not** match the game.

### Not implemented yet

The following can be met in a single-player run but are missing or incomplete:

- **Some ancient relics have no effect**: ToyBox, GoldenCompass, NutritiousSoup, Driftwood, TouchOfOrobas. Ancients offer them with the original odds, but picking one does nothing or only part of what it should.
- **Upgraded starter relics** are not ported: BlackBlood, RingOfTheDrake, InfusedCore, DivineDestiny, PhylacteryUnbound (granted by TouchOfOrobas).
- **Enchantment** TezcatarasEmber is not ported.
- **Afflictions** Ringing and Entangled have no model of their own; the matching powers track them instead, and card state can differ from the game.
- **WhisperingEarring** auto-play lacks the original automatic card-selection rule.
- The victory event after the final boss (TheArchitect) is not modelled; the simulator declares the win when the final boss dies.

Multiplayer-only content, and content that exists in the game but cannot be obtained, is out of scope.

Code comments sometimes mention `Deviation #N`, `Plan ...` or document paths. These refer to the maintainers' internal deviation register and planning documents, which are not part of this repository. Where a deviation matters to users it will be published as an issue labelled `有意偏离` (intentional deviation).

## Quick start

```sh
dotnet build Sts2Sim.sln -c Release
dotnet test Sts2Sim.sln -c Release
```

Play one run with a trivial policy (a new console project referencing `src/Sts2Sim.Core`):

```csharp
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

ModelDb.Init(ContentRegistry.AllTypes);

const string seed = "EXAMPLE1";
var acts = ActDefinition.GetRandomList(seed);
var runState = new RunState(seed, acts, ascensionLevel: 10);
runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), runState));

var driver = new RunDriver(runState, new FirstChoiceDecisions());
RunDriver.Result result = await driver.RunAsync(maxFloors: 60);
Console.WriteLine($"Outcome={result.Outcome} floors={result.FloorsVisited} hp={result.FinalPlayerHp}");

// Only map and combat choices are required; rewards, shops, rest sites and
// events fall back to the interface's default policies.
sealed class FirstChoiceDecisions : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
}
```

A policy that only ends its turn dies on the first floor, of course. Implement `IRunDecisionSource` to plug in your own agent; the tests under `tests/Sts2Sim.Core.Tests` show many more entry points (single combats, events, shops, specific cards and relics).

## Versioning

Release tags follow the game version they target. `v0.111.0` is the first release for game v0.111.0; later simulator fixes on the same game baseline are `v0.111.0.1`, `v0.111.0.2`, and so on. When the game updates, numbering restarts at the new game version (for example `v0.112.0`). The first three numbers therefore always tell you which game version to compare against.

## Reporting issues and contributing

- Behaviour that differs from the game: open a **保真差异报告** (fidelity report) with the game version, seed, character, ascension and the steps to reproduce.
- Other bugs: open a **Bug 报告** (bug report).
- Pull requests are welcome. This repository is exported from a private upstream, so accepted changes are applied there and appear here in the next export, with you credited as co-author. See [CONTRIBUTING.md](CONTRIBUTING.md) (in Chinese); AI coding assistants should follow [AGENTS.md](AGENTS.md).

Issues and pull requests may be written in English or Chinese. For day-to-day discussion, join the developer QQ group **[1106541324](https://qm.qq.com/q/f7BKSECnzU)** (mostly Chinese-speaking).

## Contributors

This simulator was developed by the following people. The public history starts at the first export, so earlier commits are not visible here:

- **[@iRyougi](https://github.com/iRyougi)**: maintainer; core port, RNG and seeding, map and run flow, comparison tooling.
- **[@ltlly](https://github.com/ltlly)**: ported Ironclad and Defect (including orbs); many fidelity fixes across damage and death resolution, generated-card creators, hook order, transforms and rewards, and monster move graphs.
- **[@s1f102500012](https://github.com/s1f102500012)**: end-of-turn card order and ethereal exhaust, shuffle order of same-ID cards, draws and shuffles after combat ends, v0.111.0 card values, the PunchOff event; ported Necrobinder (including the Osty summon and two-phase HP loss).
- **[@Charlie-chulong](https://github.com/Charlie-chulong)**: pet system (Byrdpip, Pael's Legion), MysteriousKnight and Lantern Key combat, monster move IDs and monster RNG seeding.

## License

[MIT](LICENSE). Slay the Spire 2 and its content are the property of Mega Crit.
