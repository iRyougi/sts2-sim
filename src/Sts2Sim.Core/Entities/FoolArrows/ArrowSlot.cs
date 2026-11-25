using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Entities.FoolArrows;

/// <summary>Fool 箭矢槽实体（原版 <c>MegaCrit.Sts2.Core.Entities.FoolArrows.ArrowSlot</c>）。</summary>
public sealed class ArrowSlot(Player owner)
{
    public Player Owner { get; } = owner;

    public bool IsAvailable => Status == ArrowStatus.Retrieved;

    public ArrowStatus Status { get; set; }
}
