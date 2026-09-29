using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Powers;

public sealed class NightmarePower : PowerModel
{
    private bool _hasSelectedCard;
    private ModelId _selectedCardId = ModelId.none;
    private int _selectedCardUpgradeLevel;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public void SetSelectedCard(CardModel card)
    {
        _hasSelectedCard = true;
        _selectedCardId = card.Id;
        _selectedCardUpgradeLevel = card.CurrentUpgradeLevel;
    }

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player || !_hasSelectedCard)
        {
            return;
        }

        CardModel canonical = ModelDb.GetById<CardModel>(_selectedCardId);
        for (int index = 0; index < Amount; index++)
        {
            var copy = (CardModel)canonical.MutableClone();
            copy.AssignOwner(player);
            for (int level = 0; level < _selectedCardUpgradeLevel; level++)
            {
                copy.Upgrade();
            }

            await CardPileCmd.Generate(Owner.CombatState!, copy, PileType.Hand, Owner.Player);
        }

        await PowerCmd.Remove(this);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_hasSelectedCard);
        if (_hasSelectedCard)
        {
            builder.Append(_selectedCardId);
            builder.Append(_selectedCardUpgradeLevel);
        }
    }
}
