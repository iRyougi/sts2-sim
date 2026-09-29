namespace Sts2Sim.Core.Reporting;

public sealed record ShopFloorDetail(IReadOnlyList<PurchaseRecord> Purchases) : FloorDetail;
