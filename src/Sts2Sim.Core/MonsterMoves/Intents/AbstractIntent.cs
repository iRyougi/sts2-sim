namespace Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>怪物意图的数据面。偏离 #34：剥离全部 Godot UI 成员(Texture2D/HoverTip/精灵路径/动画名)。</summary>
public abstract class AbstractIntent
{
    public abstract IntentType IntentType { get; }
}
