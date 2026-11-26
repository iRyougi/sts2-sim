namespace Sts2Sim.Core.Events.Display;

/// <summary>
/// One of the game event's DynamicVars (<c>EventModel.DynamicVars</c>, CanonicalVars order) with its current BaseValue as
/// the simulator can reconstruct it. <see cref="VarType"/> is the game's DynamicVar class name (<c>GoldVar</c>,
/// <c>HpLossVar</c>, …). A <c>StringVar</c> carries text in the game; here <see cref="IsString"/> is true and
/// <see cref="BaseValue"/> is 0. Read-only display data (偏离 #82): nothing in the simulation reads it.
/// </summary>
public sealed record EventDynamicVar(string Name, string VarType, decimal BaseValue, bool IsString);
