using System.Text.Json.Serialization;

namespace Sts2Sim.Core.Reporting;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CombatFloorDetail), "combat")]
[JsonDerivedType(typeof(RestSiteFloorDetail), "rest_site")]
[JsonDerivedType(typeof(ShopFloorDetail), "shop")]
[JsonDerivedType(typeof(TreasureFloorDetail), "treasure")]
[JsonDerivedType(typeof(EventFloorDetail), "event")]
[JsonDerivedType(typeof(AncientFloorDetail), "ancient")]
public abstract record FloorDetail;
