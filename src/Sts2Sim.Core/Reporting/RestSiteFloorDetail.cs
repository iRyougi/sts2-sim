namespace Sts2Sim.Core.Reporting;

public sealed record RestSiteFloorDetail(string Decision, int? HealAmount, string? UpgradedCard) : FloorDetail;
