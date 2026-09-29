using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event offering a player-selected deck transformation or upgrade. 偏离 #149：省略
/// MAINTAIN_CONTROL 分支的 aromaPrinciple 纯本地化文本；无玩法影响。</summary>
public sealed class AromaOfChaos : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("LET_GO", LetGoAsync),
        new EventOption("MAINTAIN_CONTROL", MaintainControlAsync),
    };

    private async Task LetGoAsync()
    {
        CardModel? card = (await CardSelectCmd.FromDeckForTransformation(Owner, 1, this)).FirstOrDefault();
        if (card is not null)
        {
            await CardCmd.TransformToRandom(card, Rng, RunState);
        }

        Finish();
    }

    private async Task MaintainControlAsync()
    {
        CardModel? card = (await CardSelectCmd.FromDeckForUpgrade(Owner, 1, this)).FirstOrDefault();
        if (card is not null)
        {
            CardCmd.Upgrade(card);
        }

        Finish();
    }
}
