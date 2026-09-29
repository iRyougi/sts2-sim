# sts2-sim

An unofficial, headless simulator for **Slay the Spire 2**, written in C# (.NET 9).

This project is not affiliated with or endorsed by Mega Crit. It contains no game files, assets or localization text. Building and testing it does not require the game; comparing its behaviour against the real game requires your own legitimate copy.

## What it is

- **A port of the game logic, not a re-design.** Game rules are ported from the game's own code as literally as practical, so behaviour can be checked method by method. Doc comments cite the original type they come from (for example `MegaCrit.Sts2.Core.Commands.CardCmd`).
- **Seed-exact RNG.** The random number generator and seed derivation follow the game, with the goal that the same seed and the same choices reproduce the same run as the game client, draw for draw.
- **Cloneable state.** Runs and combats can be cloned (`CloneExact`) for search, or cloned with fresh randomness (`CloneReseeded`) for sampling.
- **Full runs.** Map generation, Neow and other ancients, combat, elites and bosses, events, shops, rest sites, treasure, rewards, potions, relics, enchantments, and ascension levels 0–10.

| | |
|---|---|
| Game baseline | STS2 v0.111.0 (release commit `41cef1ea`, `main_assembly_hash 222455745`) |
| Characters | Defect, Ironclad, Regent, Silent |
| Acts | Overgrowth, Underdocks, Hive, Glory |
| Multiplayer | Not supported |

## Fidelity status — please read

The sequential RNG mode **aims** for bit-for-bit agreement with the game. It has been validated by replaying recorded games from the real client and a headless game host and diffing the results, and those comparisons are how most bugs here were found. **Known discrepancies remain and not all of them are fixed.** Do not assume a result is identical to the game without checking; if you find a difference, please report it (see below).

There are two RNG modes, and they are deliberately separate APIs:

- **Sequential** (default): matches the game. Use this for anything that must agree with the client.
- **Keyed** (`RunState.CreateKeyedForLabels`): an intentional deviation. Random draws are keyed by purpose instead of drawn in sequence, so two branches of a run stay comparable. Use it only for paired comparisons and training-data generation; it does **not** match the game.

Code comments sometimes mention `Deviation #N`, `Plan ...` or document paths. These refer to the maintainers' internal deviation register and planning documents, which are not part of this repository. Where a deviation matters to users it will be published as an issue labelled `known-deviation`.

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

- Behaviour that differs from the game: open a **Fidelity report** with the game version, seed, character, ascension and the steps to reproduce.
- Other bugs: open a **Bug report**.
- Pull requests are welcome. This repository is exported from a private upstream, so accepted changes are applied there and appear here in the next export, with you credited as co-author. See [CONTRIBUTING.md](CONTRIBUTING.md).

Issues and pull requests may be written in English or Chinese.

## License

[MIT](LICENSE). Slay the Spire 2 and its content are the property of Mega Crit.
