using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.PotionPools;

namespace Sts2Sim.Core.Models;

/// <summary>
/// 角色基类。只保留玩法字段(偏离 #14):Loc/贴图/动画/音效全部剔除;
/// CardPool/RelicPool/PotionPool/StartingDeck/StartingRelics/StartingPotions
/// 依赖 Plan 03 的 CardModel/RelicModel/PotionModel 与池类型,届时按原文补充。
/// </summary>
public abstract class CharacterModel : AbstractModel
{
    public virtual bool IsPlayable => true;

    public abstract int StartingHp { get; }

    public abstract int StartingGold { get; }

    public virtual int MaxEnergy => 3;

    public virtual int BaseOrbSlotCount => 0;

    public override bool ShouldReceiveCombatHooks => false;

    /// <summary>起始牌组的卡类型列表。</summary>
    public virtual IReadOnlyList<Type> StartingDeck => Array.Empty<Type>();

    /// <summary>起始遗物类型列表；当前只建模储君内容。</summary>
    public virtual IReadOnlyList<Type> StartingRelics => Array.Empty<Type>();

    /// <summary>The character's explicit official card-pool membership.</summary>
    public virtual CardPoolModel CardPool => EmptyCardPool.Instance;

    /// <summary>角色专属遗物池。奖励池 = 本池 + SharedRelicPool。</summary>
    public virtual RelicPoolModel RelicPool => EmptyRelicPool.Instance;

    public virtual PotionPoolModel PotionPool => EmptyPotionPool.Instance;

}
