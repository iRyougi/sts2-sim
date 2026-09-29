using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DustyTome : RelicModel
{
    private ModelId? _ancientCard;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public ModelId? AncientCard
    {
        get => _ancientCard;
        set { AssertMutable(); _ancientCard = value; }
    }

    public void SetupForPlayer(Player player)
    {
        IEnumerable<CardModel> candidates = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1)
            .Where(card => card.Rarity == CardRarity.Ancient && !ArchaicTooth.TranscendenceCards.Contains(card));
        AncientCard = (player.PlayerRng.Rewards.NextItem(candidates)
            ?? throw new InvalidOperationException("No eligible Ancient card for DustyTome.")).Id;
    }

    public override async Task AfterObtained()
    {
        var card = (CardModel)ModelDb.GetById<CardModel>(
            AncientCard ?? throw new InvalidOperationException("DustyTome must be set up before obtaining it.")).MutableClone();
        card.AssignOwner(Owner);
        CardCmd.Upgrade(card);
        await CardPileCmd.AddToDeck(card);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_ancientCard?.ToString());
}
