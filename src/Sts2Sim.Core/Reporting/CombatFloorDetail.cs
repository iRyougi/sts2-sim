namespace Sts2Sim.Core.Reporting;

public sealed record CombatFloorDetail(string CombatId, string CombatLogFile, bool Victory) : FloorDetail;
