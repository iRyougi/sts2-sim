<div align="center">

# sts2-sim

**中文** | [English](README.en.md)

[![CI](https://img.shields.io/github/actions/workflow/status/iRyougi/sts2-sim/ci.yml?branch=main&label=CI&logo=githubactions&logoColor=white)](https://github.com/iRyougi/sts2-sim/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/iRyougi/sts2-sim?label=release&color=blue)](https://github.com/iRyougi/sts2-sim/releases/latest)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![STS2 v0.111.0](https://img.shields.io/badge/%E6%B8%B8%E6%88%8F%E5%9F%BA%E7%BA%BF-STS2%20v0.111.0-8B0000)](#)
[![License: MIT](https://img.shields.io/github/license/iRyougi/sts2-sim?color=green)](LICENSE)
[![保真差异](https://img.shields.io/github/issues/iRyougi/sts2-sim/%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82?label=%E6%9C%AA%E4%BF%AE%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82&color=orange)](https://github.com/iRyougi/sts2-sim/issues?q=is%3Aissue+is%3Aopen+label%3A%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82)
[![Stars](https://img.shields.io/github/stars/iRyougi/sts2-sim?style=flat&logo=github)](https://github.com/iRyougi/sts2-sim/stargazers)
[![QQ 1106541324](https://img.shields.io/badge/QQ%E7%BE%A4-1106541324-12B7F5?logo=tencentqq&logoColor=white)](https://qm.qq.com/q/f7BKSECnzU)

**开发交流 QQ 群：[1106541324](https://qm.qq.com/q/f7BKSECnzU)**（Torch塔2mod交流群；使用、移植、保真差异都可以来聊）

</div>

《杀戮尖塔2》（Slay the Spire 2）的非官方 headless 模拟器，用 C#（.NET 9）编写。

本项目与 Mega Crit 无关，也未获其认可。仓库不含任何游戏文件、美术资源或本地化文本。构建和测试不需要游戏；如果要和真实游戏对比行为，需要自备正版游戏。

## 这是什么

- **照游戏代码移植，不是重新设计。** 游戏规则尽可能逐字从游戏自身的代码移植过来，因此可以逐个方法对照核对。文档注释会标出对应的原始类型（例如 `MegaCrit.Sts2.Core.Commands.CardCmd`）。
- **随机数与种子一致。** 随机数生成器和种子派生都照游戏实现，目标是：同样的种子、同样的选择，得到和游戏客户端逐次抽取都一致的对局。
- **可克隆。** 战斗状态可以克隆（`CombatState.Clone()`），用于搜索；随机数流（`Rng`、`RunRngSet`、`PlayerRngSet`）可以原样复制（`CloneExact`），也可以换一套新随机数复制（`CloneReseeded`），用于采样。整局状态（`RunState`）目前还不支持深克隆。
- **完整对局。** 地图生成、涅奥等先古之民、战斗、精英与 Boss、事件、商店、休息处、宝箱、奖励、药水、遗物、附魔，以及进阶 0–10 级。游戏 v0.111.0 的全部卡牌和药水、四幕的全部遭遇与事件都已移植；少数内容尚未实现，见下文。

| | |
|---|---|
| 游戏基线 | STS2 v0.111.0（release commit `41cef1ea`，`main_assembly_hash 222455745`） |
| 角色 | Defect, Ironclad, Necrobinder, Regent, Silent |
| 幕 | Overgrowth、Underdocks、Hive、Glory |
| 多人模式 | 不支持 |

## 保真现状（请先读）

顺序随机数模式**以**与游戏逐位一致**为目标**。我们用真实客户端和 headless 游戏宿主录下的对局做回放、逐项比对结果，这里修掉的大部分问题都是这样发现的。**但仍有已知的不一致，没有全部修复**，列表见带 [`保真差异`](https://github.com/iRyougi/sts2-sim/issues?q=is%3Aissue+label%3A%E4%BF%9D%E7%9C%9F%E5%B7%AE%E5%BC%82) 标签的议题。 不要未经核对就假定结果和游戏完全相同；发现差异请报告（见下文）。

随机数有两种模式，刻意做成了互相独立的 API：

- **顺序模式**（默认）：与游戏一致。凡是需要和客户端对上的场景都用它。
- **重键模式**（`RunState.CreateKeyedForLabels`）：有意的偏离。随机抽取按用途派生，而不是按顺序抽取，这样同一局的两个分支仍然可以相互比较。只用于配对比较和生成训练数据；它与游戏**不**一致。

### 尚未实现的内容

以下内容在单人对局中能遇到，但模拟器里还没有实现或只实现了一部分：

- **部分先古之民遗物没有效果**：ToyBox、GoldenCompass、NutritiousSoup、Driftwood、TouchOfOrobas。先古之民会照原版概率把它们作为选项给出，但选了之后没有效果或效果不完整。
- **起始遗物升级版未移植**：BlackBlood、RingOfTheDrake、InfusedCore、DivineDestiny、PhylacteryUnbound（由 TouchOfOrobas 给出）。
- **附魔** TezcatarasEmber 未移植。
- 第三幕 Boss 之后的胜利事件（TheArchitect）未建模，模拟器在击败最终 Boss 时直接判定胜利。

多人模式专用的内容，以及游戏里存在但没有任何途径获得的内容，不在移植范围内。

代码注释里有时会出现 `偏离 #N`、`Plan ...` 或文档路径。这些指向维护者内部的偏离登记册和计划文档，不在本仓库中。对使用者有影响的偏离会以带 `有意偏离` 标签的议题公开。

## 快速上手

```sh
dotnet build Sts2Sim.sln -c Release
dotnet test Sts2Sim.sln -c Release
```

用一个最简单的策略跑一局（新建一个控制台项目，引用 `src/Sts2Sim.Core`）：

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

// 只有地图和战斗决策是必须实现的；奖励、商店、休息处和事件
// 会使用接口自带的默认策略。
sealed class FirstChoiceDecisions : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
}
```

只会结束回合的策略当然会死在第一层。实现 `IRunDecisionSource` 就能接入你自己的 AI；`tests/Sts2Sim.Core.Tests` 下的测试展示了更多入口（单场战斗、事件、商店、特定卡牌和遗物等）。

## 版本号

发布标签跟随所针对的游戏版本。`v0.111.0` 是针对游戏 v0.111.0 的首次发布；同一游戏基线上的后续修复依次是 `v0.111.0.1`、`v0.111.0.2`……。游戏更新后，从新的游戏版本号重新开始（例如 `v0.112.0`）。所以前三段数字永远告诉你该和哪个游戏版本对比。

## 报告问题与参与贡献

- 行为和游戏不一致：提交**保真差异报告**，写明游戏版本、种子、角色、进阶等级和复现步骤。
- 其他 bug：提交 **Bug 报告**。
- 欢迎提 PR。本仓库由私有上游仓库导出，被接受的改动会先合入上游，在下一次导出时出现在这里，并保留你的作者署名。详见 [CONTRIBUTING.md](CONTRIBUTING.md)；AI 编程助手请遵守 [AGENTS.md](AGENTS.md)。

议题和 PR 用中文或英文都可以。日常讨论可以加开发交流 QQ 群：**[1106541324](https://qm.qq.com/q/f7BKSECnzU)**。

## 贡献者

本模拟器由以下成员共同开发（按 GitHub 账号排列）。公开仓库的历史从导出开始，看不到之前的提交记录，因此在这里列出：

- **[@iRyougi](https://github.com/iRyougi)**：维护者；模拟器主体移植、随机数与种子、地图与对局流程、对拍工具链。
- **[@ltlly](https://github.com/ltlly)**：移植铁甲战士与故障机器人（含充能球机制）；大量保真修复，包括伤害与死亡结算、生成牌的 creator、钩子顺序、变牌与奖励流程、怪物招式图等。
- **[@s1f102500012](https://github.com/s1f102500012)**：回合结束时的出牌顺序与虚无消耗、同 ID 卡的洗牌顺序、战斗结束后的抽牌与洗牌、按 v0.111.0 校正卡牌数值、PunchOff 事件；移植亡灵契约师（含召唤物 Osty 与两阶段失血）。
- **[@Charlie-chulong](https://github.com/Charlie-chulong)**：宠物系统（Byrdpip、Pael's Legion）、MysteriousKnight 与 Lantern Key 战斗、怪物招式 ID 与怪物随机数种子对齐。

## 许可证

[MIT](LICENSE)。《杀戮尖塔2》及其内容归 Mega Crit 所有。
