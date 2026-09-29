using System.Globalization;
using System.Text;

namespace Sts2Sim.Core.Reporting;

/// <summary>Renders an in-memory run report as deterministic, human-readable Markdown.</summary>
public static class MarkdownReportRenderer
{
    public static string Render(
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(combatLogs);

        IReadOnlyList<CombatLogRef> references = manifest.CombatLogs ??
            throw new ArgumentException("Manifest combat log index must not be null.", nameof(manifest));
        ReportSetValidator.Validate(manifest, combatLogs);

        var markdown = new StringBuilder();
        Line(markdown, $"# Run Report: {Escape(manifest.RunId)}");
        Line(markdown);
        Line(markdown, "## Summary");
        Line(markdown);
        Line(markdown, "| Field | Value |");
        Line(markdown, "| --- | --- |");
        SummaryRow(markdown, "Schema", manifest.SchemaVersion);
        SummaryRow(markdown, "Seed", manifest.Seed);
        SummaryRow(markdown, "Character", manifest.Character);
        SummaryRow(markdown, "Ascension", Number(manifest.Ascension));
        SummaryRow(markdown, "Result", manifest.Result);
        SummaryRow(markdown, "Outcome", DescribeOutcome(manifest));
        SummaryRow(markdown, "Final HP", $"{Number(manifest.FinalHp)} / {Number(manifest.MaxHp)}");
        SummaryRow(markdown, "Floors visited", Number(manifest.FloorsVisited));
        SummaryRow(markdown, "Started (UTC)", manifest.StartedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        SummaryRow(markdown, "Ended (UTC)", manifest.EndedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Line(markdown);

        RenderFloors(markdown, manifest.Floors ?? Array.Empty<FloorEntry>());
        RenderCombats(markdown, references, combatLogs);
        return markdown.ToString();
    }

    private static void RenderFloors(StringBuilder markdown, IReadOnlyList<FloorEntry> floors)
    {
        Line(markdown, "## Floors");
        Line(markdown);
        if (floors.Count == 0)
        {
            Line(markdown, "_No floors visited._");
            Line(markdown);
            return;
        }

        Line(markdown, "| Floor | Point | Room | HP | Gold | Detail |");
        Line(markdown, "| ---: | --- | --- | --- | --- | --- |");
        foreach (FloorEntry floor in floors
                     .OrderBy(floor => floor.FloorIndex)
                     .ThenBy(floor => floor.Coord.Row)
                     .ThenBy(floor => floor.Coord.Col))
        {
            Line(
                markdown,
                $"| {Number(floor.FloorIndex)} | {Escape(floor.PointType)} | {Escape(floor.RoomType)} | " +
                $"{Number(floor.HpBefore)} → {Number(floor.HpAfter)} / {Number(floor.MaxHp)} | " +
                $"{Number(floor.GoldBefore)} → {Number(floor.GoldAfter)} | {DescribeFloorDetail(floor.Detail)} |");
        }
        Line(markdown);
        foreach (FloorEntry floor in floors
                     .OrderBy(floor => floor.FloorIndex)
                     .ThenBy(floor => floor.Coord.Row)
                     .ThenBy(floor => floor.Coord.Col))
        {
            Line(markdown, $"### Floor {Number(floor.FloorIndex)} inventory");
            Line(markdown);
            Line(markdown, $"- Deck: {DescribeValues(floor.DeckBefore)} → {DescribeValues(floor.DeckAfter)}");
            Line(markdown, $"- Relics: {DescribeValues(floor.RelicsBefore)} → {DescribeValues(floor.RelicsAfter)}");
            Line(markdown, $"- Potions: {DescribeSlots(floor.PotionsBefore)} → {DescribeSlots(floor.PotionsAfter)}");
            Line(markdown);
        }

    }

    private static void RenderCombats(
        StringBuilder markdown,
        IReadOnlyList<CombatLogRef> references,
        IReadOnlyDictionary<string, CombatLog> combatLogs)
    {
        Line(markdown, "## Combats");
        Line(markdown);
        if (references.Count == 0)
        {
            Line(markdown, "_No combats recorded._");
            Line(markdown);
            return;
        }

        foreach (CombatLogRef reference in references
                     .OrderBy(reference => reference.Floor)
                     .ThenBy(reference => reference.CombatId, StringComparer.Ordinal))
        {
            CombatLog combat = combatLogs[reference.CombatId];
            Line(
                markdown,
                $"### Floor {Number(combat.Floor)} — {Escape(combat.EncounterType)} / " +
                $"{Escape(combat.EncounterName)} ({Escape(combat.CombatId)})");
            Line(markdown);
            Line(
                markdown,
                $"- Result: {Escape(combat.Result)}; duration: " +
                (combat.EndedAtUtc - combat.StartedAtUtc).ToString("c", CultureInfo.InvariantCulture));
            Line(markdown, $"- Initial player: {DescribeInitialPlayer(combat.PlayerInitial)}");
            Line(markdown, $"- Initial enemies: {DescribeInitialEnemies(combat.EnemiesInitial)}");
            Line(markdown);

            if (combat.Turns.Count == 0)
            {
                Line(markdown, "_No turns recorded._");
                Line(markdown);
            }
            else
            {
                foreach (TurnRecord turn in combat.Turns
                             .OrderBy(turn => turn.TurnIndex)
                             .ThenBy(turn => turn.Side, StringComparer.Ordinal))
                {
                    RenderTurn(markdown, turn);
                }
            }

            RenderRewards(markdown, combat.Rewards);
            Line(markdown);
        }
    }

    private static void RenderTurn(StringBuilder markdown, TurnRecord turn)
    {
        Line(markdown, $"#### Turn {Number(turn.TurnIndex)} ({Escape(turn.Side)})");
        Line(markdown);
        Line(markdown, $"- Pre: {DescribePlayer(turn.PlayerPre)}; enemies: {DescribeEnemies(turn.EnemiesPre)}");
        Line(markdown, $"- Draws: {DescribeValues(turn.Draws.Select(draw => draw.Card))}");
        if (turn.Actions.Count == 0)
        {
            Line(markdown, "- Actions: none");
        }
        else
        {
            Line(markdown, "- Actions:");
            for (int index = 0; index < turn.Actions.Count; index++)
            {
                Line(markdown, $"  {Number(index + 1)}. {DescribeAction(turn.Actions[index])}");
            }
        }
        Line(markdown, $"- Post: {DescribePlayer(turn.PlayerPost)}; enemies: {DescribeEnemies(turn.EnemiesPost)}");
        Line(markdown);
    }

    private static string DescribeAction(ActionRecord action) => action switch
    {
        PlayCardAction card =>
            $"play_card — actor=player; card={Escape(card.Card)}; target={Optional(card.Target)}; " +
            $"plays={Number(card.PlayCount)}; energy_cost={Number(card.EnergyCost)}; " +
            $"damage={DescribeDamageDealt(card.DamageDealt)}; block_gained={Number(card.BlockGained)}; " +
            $"powers={DescribePowersApplied(card.PowersApplied)}",
        UsePotionAction potion =>
            $"use_potion — actor=player; potion={Escape(potion.Potion)}; target={Optional(potion.Target)}; " +
            $"healing={Number(potion.HealingReceived)}; consumed={(potion.Consumed ? "yes" : "no")}; " +
            $"damage={DescribeDamageDealt(potion.DamageDealt)}; powers={DescribePowersApplied(potion.PowersApplied)}; " +
            $"potions_added={DescribeValues(potion.PotionsAdded)}; potions_removed={DescribeValues(potion.PotionsRemoved)}",
        EnemyAction enemy =>
            $"enemy_action — actor={Escape(enemy.Source)}; move={Escape(enemy.MoveId)}; " +
            $"damage_to_player={DescribeDamageTaken(enemy.DamageToTargets)}; powers={DescribePowersApplied(enemy.PowersApplied)}",
        EndTurnAction => "end_turn — actor=player",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported report action type."),
    };

    private static string DescribeFloorDetail(FloorDetail detail) => detail switch
    {
        CombatFloorDetail combat =>
            $"combat={Escape(combat.CombatId)}; result={(combat.Victory ? "victory" : "defeat")}; log={Escape(combat.CombatLogFile)}",
        RestSiteFloorDetail rest =>
            $"decision={Escape(rest.Decision)}; healing={Optional(rest.HealAmount)}; upgraded={Optional(rest.UpgradedCard)}",
        ShopFloorDetail shop => $"purchases={DescribePurchases(shop.Purchases)}",
        TreasureFloorDetail treasure =>
            $"gold={Number(treasure.GoldGained)}; relic={Optional(treasure.RelicGained)}",
        EventFloorDetail @event =>
            $"event={Escape(@event.EventName)}; option={Escape(@event.OptionChosen)}; effects={Escape(@event.EffectsSummary)}",
        AncientFloorDetail ancient =>
            $"ancient={Escape(ancient.EventName)}; option={Escape(ancient.OptionChosen)}",
        _ => throw new ArgumentOutOfRangeException(nameof(detail), detail, "Unsupported floor detail type."),
    };

    private static string DescribePurchases(IReadOnlyList<PurchaseRecord> purchases) =>
        purchases.Count == 0
            ? "none"
            : string.Join(
                ", ",
                purchases.Select(purchase =>
                    $"{Escape(purchase.Kind)}:{Escape(purchase.Id)}@{Number(purchase.Price)}"));

    private static string DescribeInitialPlayer(CombatPlayerInitialSnapshot player) =>
        $"hp={Number(player.Hp)}/{Number(player.MaxHp)}, block={Number(player.Block)}, " +
        $"energy={Number(player.Energy)}, deck={Number(player.DeckSize)}, " +
        $"relics={DescribeValues(player.RelicIds)}, potions={DescribeValues(player.PotionIds)}";

    private static string DescribeInitialEnemies(IReadOnlyList<CombatEnemyInitialSnapshot> enemies) =>
        enemies.Count == 0
            ? "none"
            : string.Join(
                "; ",
                enemies.Select(enemy =>
                    $"{Escape(enemy.Slot)}/{Escape(enemy.Id)} hp={Number(enemy.Hp)}/{Number(enemy.MaxHp)}, " +
                    $"powers={DescribePowers(enemy.Powers)}"));

    private static string DescribePlayer(PlayerSnapshot player) =>
        $"player hp={Number(player.Hp)}/{Number(player.MaxHp)}, block={Number(player.Block)}, " +
        $"energy={Number(player.Energy)}, stars={Number(player.Stars)}, hand={DescribeValues(player.Hand)}, " +
        $"piles=draw:{Number(player.DrawPileSize)}/discard:{Number(player.DiscardPileSize)}/exhaust:{Number(player.ExhaustPileSize)}, " +
        $"powers={DescribePowers(player.Powers)}";

    private static string DescribeEnemies(IReadOnlyList<EnemySnapshot> enemies) =>
        enemies.Count == 0
            ? "none"
            : string.Join(
                "; ",
                enemies.Select(enemy =>
                    $"{Escape(enemy.Slot)}/{Escape(enemy.Id)} hp={Number(enemy.Hp)}/{Number(enemy.MaxHp)}, " +
                    $"block={Number(enemy.Block)}, intent={Escape(enemy.Intent)}, powers={DescribePowers(enemy.Powers)}"));

    private static string DescribePowers(IReadOnlyList<PowerSnapshot> powers) =>
        powers.Count == 0
            ? "none"
            : string.Join(", ", powers.Select(power => $"{Escape(power.Id)}:{Number(power.Amount)}"));

    private static string DescribeDamageDealt(IReadOnlyList<DamageDealtRecord> damage) =>
        damage.Count == 0
            ? "none"
            : string.Join(", ", damage.Select(hit => $"{Escape(hit.Target)}:{Number(hit.Amount)}"));

    private static string DescribeDamageTaken(IReadOnlyList<DamageTakenRecord> damage) =>
        damage.Count == 0
            ? "none"
            : string.Join(
                ", ",
                damage.Select(hit =>
                    $"{Number(hit.Amount)} (block_absorbed={Number(hit.BlockAbsorbed)})"));

    private static string DescribePowersApplied(IReadOnlyList<PowerApplicationRecord> powers) =>
        powers.Count == 0
            ? "none"
            : string.Join(
                ", ",
                powers.Select(power =>
                    $"{Escape(power.Target)}:{Escape(power.Power)}+{Number(power.Amount)}"));

    private static void RenderRewards(StringBuilder markdown, CombatRewards rewards)
    {
        IReadOnlyList<string> cardsTaken = WithLegacyFallback(rewards.CardsTaken, rewards.CardTaken);
        IReadOnlyList<string> relicsTaken = WithLegacyFallback(rewards.RelicsTaken, rewards.RelicTaken);
        IReadOnlyList<string> potionsTaken = WithLegacyFallback(rewards.PotionsTaken, rewards.PotionTaken);
        Line(
            markdown,
            $"- Rewards: gold={Number(rewards.Gold)}; offered={DescribeValues(rewards.CardsOffered)}; " +
            $"cards_taken={DescribeValues(cardsTaken)}; relics_taken={DescribeValues(relicsTaken)}; " +
            $"potions_taken={DescribeValues(potionsTaken)}");
    }

    private static IReadOnlyList<string> WithLegacyFallback(
        IReadOnlyList<string> plural,
        string? singular) => plural.Count > 0 || singular is null ? plural : [singular];

    private static string DescribeValues(IEnumerable<string?> values)
    {
        string[] escaped = values
            .Where(value => value is not null)
            .Select(value => Escape(value!))
            .ToArray();
        return escaped.Length == 0 ? "none" : string.Join(", ", escaped);
    }

    private static string DescribeSlots(IEnumerable<string?> values) =>
        $"[{string.Join(", ", values.Select(value => value is null ? "empty" : Escape(value)))}]";

    private static string Optional(string? value) => value is null ? "none" : Escape(value);

    private static string Optional(int? value) => value.HasValue ? Number(value.Value) : "none";

    /// <summary><c>Result</c> 只说"是否通关"，把死亡与 <c>maxFloors</c> 截断压成同一个 defeat。
    /// 这一行把真实结局摊开，避免读报告的人（或统计脚本）把两者混为一谈。</summary>
    private static string DescribeOutcome(RunManifest manifest)
    {
        string ending =
            manifest.Result == "victory" ? "cleared the final act"
            : !manifest.Survived ? "died"
            : manifest.Truncated ? "still alive, stopped at the floor limit"
            : "still alive, ran out of reachable map";
        return $"{ending}; reached a boss: {(manifest.ReachedBoss ? "yes" : "no")}; " +
            $"acts cleared: {Number(manifest.ActsCleared)}";
    }

    private static void SummaryRow(StringBuilder markdown, string field, string value) =>
        Line(markdown, $"| {field} | {Escape(value)} |");

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>`_` is intentionally left unescaped: CommonMark never treats an intraword
    /// underscore as emphasis, and every model ID rendered here is SCREAMING_SNAKE_CASE.</summary>
    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("#", "&#35;", StringComparison.Ordinal)
        .Replace("\\", "&#92;", StringComparison.Ordinal)
        .Replace("|", "&#124;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\r\n", "<br>", StringComparison.Ordinal)
        .Replace("\r", "<br>", StringComparison.Ordinal)
        .Replace("\n", "<br>", StringComparison.Ordinal)
        .Replace("*", "&#42;", StringComparison.Ordinal)
        .Replace("~", "&#126;", StringComparison.Ordinal)
        .Replace("`", "&#96;", StringComparison.Ordinal)
        .Replace("[", "&#91;", StringComparison.Ordinal)
        .Replace("]", "&#93;", StringComparison.Ordinal);

    private static void Line(StringBuilder markdown, string value = "") =>
        markdown.Append(value).Append('\n');
}
