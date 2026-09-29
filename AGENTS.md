# AGENTS.md — sts2-sim

Rules for everyone working in this repository, human or AI assistant (Codex, Claude Code, and others). Read this before making changes; it takes precedence over your default habits. [CONTRIBUTING.md](CONTRIBUTING.md) describes how contributions are reviewed and released.

## 1. What this project is

A headless Slay the Spire 2 simulator, ported from the game's own code. The goal is that, in sequential RNG mode, the same seed and the same choices reproduce the same run as the game client, draw for draw. The current game baseline is in the README.

## 2. Fidelity rules

1. **Port literally.** The game's code is the specification. Do not "improve", simplify or reinterpret game rules. Any place where the simulator knowingly differs from the game is an intentional deviation and must be stated in the pull request: the game's behaviour, what the simulator does instead, and why.
2. **Watch overloads and default parameters.** Methods with the same name can take integer arguments that mean different things in different overloads (for example a maximum repeat count in one and a cooldown in another, not a weight). Port the exact call the game makes; never rewrite a call by what you think it means.
3. **Fix the first divergence, not the symptom.** When the simulator disagrees with the game, find the first point where they diverge and fix that. Never add RNG draws, remove or reorder draws, change seeds, or loosen a comparison to make a result match.
4. **A green test suite is not proof of fidelity.** Tests have been found that were written against the simulator's own mistaken behaviour and locked the mistake in. If you find one, correct the assertion to match the game.
5. **Everything is unlocked.** The simulator always behaves as if every unlock, achievement and epoch in the game is complete. Do not port unlock tracking. This does not apply to live run state (gold, HP, deck contents): gates that depend on the current run must be ported as written.
6. **Keep the RNG modes separate.** Sequential mode matches the game. Keyed mode (`RunState.CreateKeyedForLabels`) is an intentional deviation used only for paired comparisons and training data. `CloneExact`, `CloneReseeded` and the keyed APIs are deliberately separate methods: **do not merge them behind a boolean parameter.** Choosing the wrong one does not fail; it silently produces wrong results that are very hard to trace.
7. **Be careful with shared engine code.** Changes to `src/Sts2Sim.Core/Random/Rng.cs`, hook dispatch, and combat state cloning affect everything downstream. Keep such changes minimal and explain them.

## 3. What must never be committed

- Game assemblies, executables, decompiled source files, game assets, or localization text.
- Large pasted blocks of decompiled game code, in code comments, issues or pull requests. Citing the original type or method name (for example `MegaCrit.Sts2.Core.Commands.CardCmd`) is fine and encouraged.
- Local machine paths, credentials, or large raw experiment outputs (logs, captures, big JSON files).

## 4. Tests

Tests cost run time and, for AI assistants, context. Keep them lean:

1. Test only the behaviour you changed. Do not test the framework or existing code, and do not restate the same invariant in a second test.
2. Use `[Theory]` with `[InlineData]` for several levels or parameters, not one `[Fact]` per case.
3. Check everything one fixture can show in one test, with assertion messages that carry the seed and position so a failure is easy to locate.
4. A test is worth keeping only if its failure would point to a specific change that breaks behaviour.
5. Run targeted tests while working (`--filter`); run the full suite once before opening a pull request.
6. Long measurements and experiments are not unit tests. Keep them out of the test project.

Some tests are opt-in and skipped by default; they are enabled by an environment variable named in their skip message.

## 5. Build and test

Requires the .NET 9 SDK. No game files are needed.

```sh
dotnet build Sts2Sim.sln -c Release
dotnet test Sts2Sim.sln -c Release
dotnet test Sts2Sim.sln -c Release --filter "FullyQualifiedName~<ClassName>"
```

## 6. Code comments

Comments may mention `Deviation #N`, `Plan ...` or document paths. These refer to the maintainers' internal deviation register and planning documents, which are not in this repository. Leave them as they are; do not invent content for them.

## 7. Commits and pull requests

- Commit messages: `<type>: <description>`, where type is one of `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`.
- One pull request per change. For fidelity fixes, name the game type and method you followed and the seed or case that showed the difference.
- This repository is exported from a private upstream. Accepted pull requests are applied upstream and appear here in the next export; see CONTRIBUTING.md.
