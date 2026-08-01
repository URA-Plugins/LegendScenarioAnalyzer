using System.Text;
using UmamusumeResponseAnalyzer.TerminalGui;

namespace LegendScenarioAnalyzer;

internal sealed class LegendTrainingDisplayBuilder
{
    public LegendScenarioStage Stage { get; private init; }
    public List<LegendDisplayPanel> HeaderPanels { get; } = [];
    public List<LegendDisplayPanel> ScenarioPanels { get; } = [];
    public List<string> ImportantRows { get; } = [];
    public List<LegendTrainingCard> TrainingCards { get; } = [];
    public List<LegendSelectionCard> SelectionCards { get; } = [];
    public List<string> ExtraRows { get; } = [];

    public LegendTrainingCard? FindTrainingCardByTrainIndex(int trainIndex)
        => TrainingCards.FirstOrDefault(x => x.TrainIndex == trainIndex);

    public LegendTrainingCard? FindTrainingCardByCommandId(int commandId)
        => TrainingCards.FirstOrDefault(x => x.CommandId == commandId);

    public LegendSelectionCard? FindSelectionCardByBuffId(int buffId)
        => SelectionCards.FirstOrDefault(x => x.BuffId == buffId);

    public LegendSelectionCard? FindSelectionCard(LegendBuffColor color, int ordinalWithinColor)
        => SelectionCards.FirstOrDefault(x => x.Color == color && x.OrdinalWithinColor == ordinalWithinColor);

    public LegendDisplayPanel? FindScenarioPanel(string key)
        => ScenarioPanels.FirstOrDefault(x => x.Key == key);

    public static LegendTrainingDisplayBuilder CreateDefault(LegendTrainingDisplayContext context)
    {
        var builder = new LegendTrainingDisplayBuilder { Stage = context.ResponseData.Stage };
        var turn = context.Turn;
        var data = context.ResponseData;
        var totalValue = turn.StatsRevised.Sum();

        builder.HeaderPanels.Add(new("date", "日期", $"{turn.Year}{LegendDisplayText.Year} {turn.Month}{LegendDisplayText.Month}{turn.HalfMonth}"));
        builder.HeaderPanels.Add(new("total", "总属性", $"总属性: {totalValue}, Pt: {data.CharaInfo.skill_point}"));
        builder.HeaderPanels.Add(new("vital", "体力", $"{LegendDisplayText.Vital}: {turn.Vital}/{turn.MaxVital}"));
        builder.HeaderPanels.Add(new("motivation", "干劲", LegendDisplayText.Motivation(data.CharaInfo.motivation)));

        AddImportantRows(context, builder);
        AddScenarioPanels(context, builder);
        if (data.Stage == LegendScenarioStage.Training)
            AddTrainingCards(context, builder);
        else
            AddNonTrainingRows(context, builder);
        return builder;
    }

    static void AddImportantRows(LegendTrainingDisplayContext context, LegendTrainingDisplayBuilder builder)
    {
        var turn = context.Turn;
        var data = context.ResponseData;
        if (context.PreviousTurn != turn.Turn - 1 && context.PreviousTurn != turn.Turn && turn.Turn != 1)
            builder.ImportantRows.Add(LegendDisplayText.WrongTurnAlert(context.PreviousTurn, turn.Turn));

        var availableTrainingCount = data.HomeInfo!.command_info_array.Count(x => x.is_enable == 1);
        if (availableTrainingCount <= 1)
            builder.ImportantRows.Add($"非训练回合 playingState = {data.CharaInfo.playing_state}");
        if (data.Stage != LegendScenarioStage.Training)
            builder.ImportantRows.Add($"非训练阶段: {LegendDisplayText.StageName(data.Stage)}");
        if (data.CharaInfo.skill_point > 9500)
            builder.ImportantRows.Add("剩余PT>9500（上限9999），请及时学习技能");
    }

    static void AddScenarioPanels(LegendTrainingDisplayContext context, LegendTrainingDisplayBuilder builder)
    {
        var turn = context.Turn;
        var dataSet = context.DataSet;
        var buffPeriod = (turn.Turn - 1) % 6 + 1;
        builder.ScenarioPanels.Add(new("buff-period", "心得周期", $"心得回合周期 {buffPeriod}/6"));

        var gaugeCounts = turn.GaugeCounts;
        builder.ScenarioPanels.Add(new(
            "gauge-level",
            "心得等级",
            $"蓝 {gaugeCounts[9046]}/8 绿 {gaugeCounts[9047]}/8 红 {gaugeCounts[9048]}/8"));
        builder.ScenarioPanels.Add(new("buff-color", "心得颜色", CreateBuffColorInfo(turn, dataSet)));
    }

    static string CreateBuffColorInfo(TurnInfoLegend turn, Gallop.SingleModeLegendDataSet dataSet)
    {
        if (turn.Turn > 36 && dataSet.masterly_bonus_info is not null)
        {
            var mainColor =
                dataSet.masterly_bonus_info.info_9046 is not null ? 1 :
                dataSet.masterly_bonus_info.info_9047 is not null ? 2 :
                dataSet.masterly_bonus_info.info_9048 is not null ? 3 : 0;
            return $"主色：{LegendColors.Name(mainColor)}";
        }

        var blueBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 1);
        var greenBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 2);
        var redBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 3);
        return $"当前心得颜色：蓝 {blueBuffCount} 绿 {greenBuffCount} 红 {redBuffCount}";
    }

    static void AddTrainingCards(LegendTrainingDisplayContext context, LegendTrainingDisplayBuilder builder)
    {
        var maxScore = context.TrainStats.Count == 0 ? 0 : context.TrainStats.Max(x => x.FiveValueGain.Sum());
        foreach (var command in context.Turn.CommandInfoArray)
        {
            var stats = context.TrainStats[command.TrainIndex - 1];
            builder.TrainingCards.Add(CreateTrainingCard(context.Turn, command, stats, maxScore));
        }

        if (context.ResponseData.CharaInfo.chara_effect_id_array.Any(x => x == 104))
            builder.ExtraRows.Add("团卡彩圈生效中");
    }

    static LegendTrainingCard CreateTrainingCard(TurnInfoLegend turn, LegendCommandInfo command, TrainStats stats, int maxScore)
    {
        var failureRate = stats.FailureRate > 0 ? $" ({stats.FailureRate}%)" : string.Empty;
        var card = new LegendTrainingCard(command.CommandId, command.TrainIndex)
        {
            Title = $"{LegendDisplayText.TrainName(command.TrainIndex)}{failureRate}"
        };

        var currentStat = turn.StatsRevised[command.TrainIndex - 1];
        var statUpToMax = turn.MaxStatsRevised[command.TrainIndex - 1] - currentStat;
        card.AddRow(LegendDisplayText.CurrentRemainStat);
        card.AddRow($"{currentStat}:{statUpToMax}");
        card.AddRule();

        var afterVital = stats.VitalGain + turn.Vital;
        card.AddRow($"{LegendDisplayText.Vital}:{afterVital}/{turn.MaxVital}");
        var gaugeGain = command.GaugeGain;
        var gaugeId = gaugeGain.LegendId - 9045;
        card.AddRow($"Lv{command.TrainLevel} | {LegendColors.Name(gaugeId)} {turn.GaugeCounts[gaugeGain.LegendId]}+{gaugeGain.GainGauge}");
        card.AddRule();

        var score = stats.FiveValueGain.Sum();
        card.AddRow($"{LegendDisplayText.StatSimple}:{score}|Pt:{stats.PtGain}{(score == maxScore ? " ★" : string.Empty)}");
        foreach (var trainingPartner in command.TrainingPartners)
        {
            card.AddRow(trainingPartner.Name);
            card.Highlighted |= trainingPartner.Shining;
        }
        for (var i = 5 - command.TrainingPartners.Count; i > 0; i--)
            card.AddRow(string.Empty);
        return card;
    }

    static void AddNonTrainingRows(LegendTrainingDisplayContext context, LegendTrainingDisplayBuilder builder)
    {
        builder.ExtraRows.Add($"非训练阶段: {LegendDisplayText.StageName(context.ResponseData.Stage)}");
        if (context.ResponseData.Stage != LegendScenarioStage.BuffSelection)
            return;

        GameGlobal.LoadLegendBuffs();
        var buffs = context.DataSet.obtainable_buff_id_array
            .Select(RequireLegendBuff)
            .OrderBy(x => x.color)
            .ThenByDescending(x => x.rank)
            .ThenBy(x => x.buffId);
        var ordinalsByColor = new Dictionary<LegendBuffColor, int>();
        foreach (var buff in buffs)
        {
            var color = ToBuffColor(buff.color);
            var ordinal = ordinalsByColor.GetValueOrDefault(color) + 1;
            ordinalsByColor[color] = ordinal;
            builder.SelectionCards.Add(new(buff.buffId, color, ordinal, buff.name, buff.cn_effect, buff.rank));
        }
    }

    static LegendBuff RequireLegendBuff(int buffId)
        => GameGlobal.LegendBuffInfo.FirstOrDefault(x => x.buffId == buffId)
            ?? throw new InvalidDataException($"legend_buff.csv 缺少 buffId={buffId} 的心得数据。");

    static LegendBuffColor ToBuffColor(int color) => color switch
    {
        0 => LegendBuffColor.Blue,
        1 => LegendBuffColor.Green,
        2 => LegendBuffColor.Red,
        _ => throw new InvalidDataException($"legend_buff.csv 包含未知心得颜色: color={color}")
    };
}

internal static class LegendTrainingDisplayRenderer
{
    public static WorkspaceContent Render(LegendTrainingDisplayBuilder builder)
        => WorkspaceContent.Text(RenderText(builder));

    static string RenderText(LegendTrainingDisplayBuilder builder)
    {
        var lines = new List<string>
        {
            string.Join(" | ", builder.HeaderPanels.Select(x => x.Content)),
        };
        AppendSection(lines, "重要信息", builder.ImportantRows);
        AppendSection(lines, "剧本信息", builder.ScenarioPanels.Select(x => $"{x.Title}: {x.Content}"));

        if (builder.TrainingCards.Count != 0)
        {
            lines.Add(string.Empty);
            lines.Add("== 训练信息 ==");
            foreach (var card in builder.TrainingCards)
            {
                lines.Add($"{(card.Highlighted ? "▶ " : string.Empty)}[{card.Title}]");
                lines.AddRange(card.Rows.Select(x => $"  {x}"));
                lines.Add(string.Empty);
            }
        }
        else if (builder.SelectionCards.Count != 0)
        {
            lines.Add(string.Empty);
            lines.Add("== 心得选择 ==");
            lines.AddRange(builder.SelectionCards.Select(RenderSelection));
        }
        else
        {
            lines.Add(string.Empty);
            lines.Add("非训练阶段");
        }

        AppendSection(lines, "Extras", builder.ExtraRows);
        return string.Join(Environment.NewLine, lines);
    }

    static void AppendSection(List<string> output, string title, IEnumerable<string> rows)
    {
        var values = rows.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (values.Length == 0)
            return;
        output.Add(string.Empty);
        output.Add($"== {title} ==");
        output.AddRange(values);
    }

    static string RenderSelection(LegendSelectionCard card)
    {
        var score = string.Empty;
        var advice = new List<string>();
        foreach (var row in card.Rows)
        {
            var text = CompactSelectionInlineText(card, NormalizeInlineText(row));
            if (text.StartsWith("AI评分:", StringComparison.Ordinal))
                score = text;
            else if (text.Length != 0)
                advice.Add(text);
        }

        return $"{(card.Highlighted ? "▶" : " ")} {LegendColors.Name((int)card.Color + 1)} ★{card.Rank} "
             + $"{card.SelectionLabel} | {card.Title} | {card.Effect} | {score} | {string.Join(" | ", advice)}".TrimEnd();
    }

    static string CompactSelectionInlineText(LegendSelectionCard card, string text)
    {
        var separatorIndex = text.IndexOfAny([':', '：']);
        if (separatorIndex < 0)
            return text;
        var key = text[..separatorIndex].Trim();
        var value = text[(separatorIndex + 1)..].Trim();
        if (key == "AI评分")
            return $"AI评分:{value}";
        if (key != "AI建议")
            return text;
        if (value.StartsWith(card.SelectionLabel, StringComparison.Ordinal))
            value = value[card.SelectionLabel.Length..].Trim();
        return $"AI建议:{value}";
    }

    static string NormalizeInlineText(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousWhitespace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                previousWhitespace = true;
                continue;
            }
            if (previousWhitespace && builder.Length != 0)
                builder.Append(' ');
            builder.Append(ch);
            previousWhitespace = false;
        }
        return builder.ToString();
    }
}

internal sealed class LegendDisplayPanel(string key, string title, string content)
{
    public string Key { get; } = key;
    public string Title { get; set; } = title;
    public string Content { get; set; } = content;
    public bool ShowHeader { get; set; } = true;
}

internal sealed class LegendTrainingCard(int commandId, int trainIndex)
{
    public int CommandId { get; } = commandId;
    public int TrainIndex { get; } = trainIndex;
    public string Title { get; set; } = commandId.ToString();
    public List<string> Rows { get; } = [];
    public bool Highlighted { get; set; }
    public void AddRow(string row) => Rows.Add(row);
    public void AddRule() => Rows.Add("────────");
}

internal sealed class LegendSelectionCard(
    int buffId,
    LegendBuffColor color,
    int ordinalWithinColor,
    string title,
    string effect,
    int rank)
{
    public int BuffId { get; } = buffId;
    public LegendBuffColor Color { get; } = color;
    public int OrdinalWithinColor { get; } = ordinalWithinColor;
    public string SelectionLabel { get; } = $"选{LegendColors.FullName(color)}第 {ordinalWithinColor} 个";
    public string Title { get; set; } = title;
    public string Effect { get; set; } = effect;
    public int Rank { get; } = rank;
    public List<string> Rows { get; } = [];
    public bool Highlighted { get; set; }
    public void AddRow(string row) => Rows.Add(row);
    public void AddRule() => Rows.Add("────────");
}

internal static class LegendColors
{
    public static string Name(int which) => which switch
    {
        1 => "蓝",
        2 => "绿",
        3 => "红",
        _ => "??"
    };

    public static string FullName(LegendBuffColor color) => color switch
    {
        LegendBuffColor.Blue => "蓝色",
        LegendBuffColor.Green => "绿色",
        LegendBuffColor.Red => "红色",
        _ => throw new InvalidDataException($"未知心得颜色: {color}")
    };
}
