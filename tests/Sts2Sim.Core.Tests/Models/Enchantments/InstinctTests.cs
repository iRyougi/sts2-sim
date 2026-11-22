using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Tests.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Enchantments;

[Collection("ModelDb")]
public sealed class InstinctTests : IDisposable
{
    public InstinctTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnchantmentDamageModifiersApplyToBaseDamageBeforeListeners()
    {
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync("fidelity-09-instinct");
        StrikeSilent card = Task11CombatTestSupport.AddToHand<StrikeSilent>(player);
        await CardCmd.Enchant<Instinct>(card, 1m);
        await PowerCmd.Apply<StrengthPower>(room.Engine.State, player.Creature, 4m, null, null);

        decimal damage = Hook.ModifyDamage(
            room.Engine.State, room.Engine.State.Enemies[0], player.Creature, 6m, ValueProp.Move, card, null, out _);

        // game Hook.ModifyDamage: Instinct's EnchantDamageMultiplicative applies to the card damage first
        // (6 x 2), then the listeners add Strength: 12 + 4, not (6 + 4) x 2.
        Assert.Equal(16m, damage);
    }
}
