using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Glam : EnchantmentModel
{
    // sls2：照原版 Glam。EnchantPlayCount 只回答“这次打几遍”，没有副作用；这张牌打出之后（AfterCardPlayed）才算本场用过。
    // 原来在 EnchantPlayCount 里就标记用过：观测编码器给手牌算重放次数、伤害预览时也会问它，问过一次再打出就只打一遍
    // （SIME0047 第三幕第一个 Boss：带华彩的寒流原版打两遍 14、sts2-sim 编码过帧后只打一遍 7）。用过仍记在 Status（Disabled）上。
    public override int EnchantPlayCount(int originalPlayCount) =>
        Status == EnchantmentStatus.Normal ? originalPlayCount + 1 : originalPlayCount;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (Status == EnchantmentStatus.Normal && ReferenceEquals(cardPlay.Card, Owner))
        {
            Status = EnchantmentStatus.Disabled;
        }
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Status = EnchantmentStatus.Normal;
    }
}
