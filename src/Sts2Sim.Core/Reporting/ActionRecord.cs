using System.Text.Json.Serialization;

namespace Sts2Sim.Core.Reporting;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
[JsonDerivedType(typeof(PlayCardAction), "play_card")]
[JsonDerivedType(typeof(UsePotionAction), "use_potion")]
[JsonDerivedType(typeof(EnemyAction), "enemy_action")]
[JsonDerivedType(typeof(EndTurnAction), "end_turn")]
public abstract record ActionRecord;
