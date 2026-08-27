namespace LegendScenarioAnalyzer;

public enum LegendDisplayColor
{
    Normal,
    Cyan,
    Green,
    Yellow,
    Red,
    DarkOrange,
    Aqua,
    Lime,
    LightGreen,
    Blue,
    Gray
}

public enum LegendDisplayStyle
{
    Normal,
    Bold
}

public readonly record struct LegendDisplaySegment(
    string Text,
    LegendDisplayColor Color = LegendDisplayColor.Normal,
    LegendDisplayStyle Style = LegendDisplayStyle.Normal);

internal sealed record LegendDisplayLine(IReadOnlyList<LegendDisplaySegment> Segments, bool IsRule = false)
{
    public string Text => string.Concat(Segments.Select(x => x.Text));

    public static LegendDisplayLine Plain(string text) => new([new(text)]);

    public static LegendDisplayLine Colored(string text, LegendDisplayColor color) => new([new(text, color)]);

    public static LegendDisplayLine Styled(params LegendDisplaySegment[] segments) => new(segments);

    public static LegendDisplayLine Rule { get; } = new([new("────────")], IsRule: true);
}

internal sealed class LegendDisplayRows : IReadOnlyList<string>
{
    readonly List<LegendDisplayLine> lines = [];

    public int Count => lines.Count;
    public string this[int index] => lines[index].Text;
    internal IReadOnlyList<LegendDisplayLine> Lines => lines;

    public void Add(string row)
    {
        foreach (var line in row.ReplaceLineEndings("\n").Split('\n'))
            Add(LegendDisplayLine.Plain(line));
    }

    public void Add(LegendDisplayLine row) => lines.Add(row);

    public IEnumerator<string> GetEnumerator() => lines.Select(x => x.Text).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed record LegendExtraSection(string Title, LegendDisplayRows Rows);

internal sealed class LegendTrainingDisplayBuilder
{
    public LegendScenarioStage Stage { get; private init; }
    public List<LegendDisplayPanel> HeaderPanels { get; } = [];
    public List<LegendDisplayPanel> ScenarioPanels { get; } = [];
    public LegendDisplayRows ImportantRows { get; } = new();
    public List<LegendTrainingCard> TrainingCards { get; } = [];
    public List<LegendSelectionCard> SelectionCards { get; } = [];
    public LegendDisplayRows ExtraRows { get; } = new();
    public List<LegendExtraSection> ExtraSections { get; } = [];

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
        builder.HeaderPanels.Add(new(
            "date",
            "日期",
            $"{turn.Year}{LegendDisplayText.Year} {turn.Month}{LegendDisplayText.Month}{turn.HalfMonth}",
            ratio: 4));
        builder.HeaderPanels.Add(new(
            "total",
            "总属性",
            LegendDisplayLine.Colored(
                $"总属性: {turn.StatsRevised.Sum()}, Pt: {data.CharaInfo.skill_point}",
                LegendDisplayColor.Cyan),
            ratio: 6));
        builder.HeaderPanels.Add(new(
            "vital",
            "体力",
            LegendDisplayLine.Styled(
                new($"{LegendDisplayText.Vital}: "),
                new(turn.Vital.ToString(), LegendDisplayColor.Green),
                new($"/{turn.MaxVital}")),
            ratio: 6));
        builder.HeaderPanels.Add(new(
            "motivation",
            "干劲",
            LegendDisplayLine.Colored(
                LegendDisplayText.Motivation(data.CharaInfo.motivation),
                data.CharaInfo.motivation switch
                {
                    5 => LegendDisplayColor.Green,
                    4 => LegendDisplayColor.Yellow,
                    _ => LegendDisplayColor.Red
                }),
            ratio: 3));

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
        {
            builder.ImportantRows.Add(LegendDisplayLine.Colored(
                LegendDisplayText.WrongTurnAlert(context.PreviousTurn, turn.Turn),
                LegendDisplayColor.Red));
        }

        var availableTrainingCount = data.HomeInfo!.command_info_array.Count(x => x.is_enable == 1);
        if (availableTrainingCount <= 1)
        {
            builder.ImportantRows.Add(LegendDisplayLine.Colored(
                $"非训练回合 playingState = {data.CharaInfo.playing_state}",
                LegendDisplayColor.Aqua));
        }
        if (data.Stage != LegendScenarioStage.Training)
        {
            builder.ImportantRows.Add(LegendDisplayLine.Colored(
                $"非训练阶段: {LegendDisplayText.StageName(data.Stage)}",
                LegendDisplayColor.Aqua));
        }
        if (data.CharaInfo.skill_point > 9500)
        {
            builder.ImportantRows.Add(LegendDisplayLine.Colored(
                "剩余PT>9500（上限9999），请及时学习技能",
                LegendDisplayColor.Red));
        }
    }

    static void AddScenarioPanels(LegendTrainingDisplayContext context, LegendTrainingDisplayBuilder builder)
    {
        var turn = context.Turn;
        var dataSet = context.DataSet;
        var buffPeriod = (turn.Turn - 1) % 6 + 1;
        builder.ScenarioPanels.Add(new(
            "buff-period",
            "心得周期",
            LegendDisplayLine.Styled(
                new("心得回合周期 "),
                new(
                    buffPeriod.ToString(),
                    buffPeriod switch
                    {
                        <= 3 => LegendDisplayColor.Normal,
                        4 => LegendDisplayColor.Yellow,
                        _ => LegendDisplayColor.Red
                    }),
                new("/6")),
            ratio: 3,
            showHeader: true));

        var gaugeCounts = turn.GaugeCounts;
        builder.ScenarioPanels.Add(new(
            "gauge-level",
            "心得等级",
            LegendDisplayLine.Styled(
                new($"{gaugeCounts[9046]}/8", LegendDisplayColor.Cyan),
                new(" "),
                new($"{gaugeCounts[9047]}/8", LegendDisplayColor.Lime),
                new(" "),
                new($"{gaugeCounts[9048]}/8", LegendDisplayColor.Red)),
            ratio: 3,
            showHeader: true));
        builder.ScenarioPanels.Add(new(
            "buff-color",
            "心得颜色",
            CreateBuffColorInfo(turn, dataSet),
            ratio: 6,
            showHeader: true));
    }

    static LegendDisplayLine CreateBuffColorInfo(TurnInfoLegend turn, Gallop.SingleModeLegendDataSet dataSet)
    {
        if (turn.Turn > 36 && dataSet.masterly_bonus_info is not null)
        {
            var mainColor =
                dataSet.masterly_bonus_info.info_9046 is not null ? 1 :
                dataSet.masterly_bonus_info.info_9047 is not null ? 2 :
                dataSet.masterly_bonus_info.info_9048 is not null ? 3 : 0;
            return LegendDisplayLine.Colored(
                $"主色：{LegendColors.Name(mainColor)}",
                LegendColors.AccentColor(mainColor));
        }

        var blueBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 1);
        var greenBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 2);
        var redBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 3);
        return LegendDisplayLine.Styled(
            new("当前心得颜色："),
            new($"蓝{blueBuffCount}", LegendDisplayColor.Cyan),
            new(" "),
            new($"绿{greenBuffCount}", LegendDisplayColor.Lime),
            new(" "),
            new($"红{redBuffCount}", LegendDisplayColor.Red));
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

    static LegendTrainingCard CreateTrainingCard(
        TurnInfoLegend turn,
        LegendCommandInfo command,
        TrainStats stats,
        int maxScore)
    {
        var card = new LegendTrainingCard(command.CommandId, command.TrainIndex)
        {
            StyledTitle = stats.FailureRate > 0
                ? LegendDisplayLine.Styled(
                    new(LegendDisplayText.TrainName(command.TrainIndex)),
                    new(
                        $"({stats.FailureRate}%)",
                        stats.FailureRate switch
                        {
                            >= 40 => LegendDisplayColor.Red,
                            >= 20 => LegendDisplayColor.DarkOrange,
                            _ => LegendDisplayColor.Yellow
                        }))
                : LegendDisplayLine.Plain(LegendDisplayText.TrainName(command.TrainIndex))
        };

        var currentStat = turn.StatsRevised[command.TrainIndex - 1];
        var statUpToMax = turn.MaxStatsRevised[command.TrainIndex - 1] - currentStat;
        card.AddRow(LegendDisplayText.CurrentRemainStat);
        card.AddRow(LegendDisplayLine.Styled(
            new($"{currentStat}:"),
            new(
                statUpToMax.ToString(),
                statUpToMax switch
                {
                    > 400 => LegendDisplayColor.Normal,
                    > 200 => LegendDisplayColor.Yellow,
                    _ => LegendDisplayColor.Red
                })));
        card.AddRule();

        var afterVital = stats.VitalGain + turn.Vital;
        card.AddRow(LegendDisplayLine.Styled(
            new($"{LegendDisplayText.Vital}:"),
            new(
                afterVital.ToString(),
                afterVital switch
                {
                    < 30 => LegendDisplayColor.Red,
                    < 50 => LegendDisplayColor.DarkOrange,
                    < 70 => LegendDisplayColor.Yellow,
                    _ => LegendDisplayColor.Green
                }),
            new($"/{turn.MaxVital}")));

        var gaugeGain = command.GaugeGain;
        var gaugeId = gaugeGain.LegendId - 9045;
        card.AddRow(LegendDisplayLine.Styled(
            new($"Lv{command.TrainLevel} | "),
            new(
                $"{LegendColors.Name(gaugeId)} {turn.GaugeCounts[gaugeGain.LegendId]}+{gaugeGain.GainGauge}",
                LegendColors.AccentColor(gaugeId))));
        card.AddRule();

        var score = stats.FiveValueGain.Sum();
        card.AddRow(LegendDisplayLine.Styled(
            new($"{LegendDisplayText.StatSimple}:"),
            new(score.ToString(), score == maxScore ? LegendDisplayColor.Aqua : LegendDisplayColor.Normal),
            new($"|Pt:{stats.PtGain}")));
        foreach (var trainingPartner in command.TrainingPartners)
        {
            card.AddRow(trainingPartner.DisplayLine);
            card.Highlighted |= trainingPartner.Shining;
        }
        for (var i = 5 - command.TrainingPartners.Count; i > 0; i--)
            card.AddRow(string.Empty);
        card.AddRule();
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

internal sealed class LegendDisplayPanel(
    string key,
    string title,
    string content,
    int ratio = 1,
    bool showHeader = false)
{
    LegendDisplayRows contentRows = CreateRows(content);

    public LegendDisplayPanel(
        string key,
        string title,
        LegendDisplayLine content,
        int ratio = 1,
        bool showHeader = false)
        : this(key, title, content.Text, ratio, showHeader)
    {
        contentRows = CreateRows(content);
    }

    public string Key { get; } = key;
    public string Title { get; set; } = title;
    public string Content
    {
        get => string.Join(Environment.NewLine, contentRows);
        set => contentRows = CreateRows(value);
    }
    public int Ratio { get; set; } = ratio;
    public bool ShowHeader { get; set; } = showHeader;
    internal IReadOnlyList<LegendDisplayLine> Lines => contentRows.Lines;

    internal void AddRow(string row) => contentRows.Add(row);
    internal void AddRow(LegendDisplayLine row) => contentRows.Add(row);

    static LegendDisplayRows CreateRows(LegendDisplayLine line)
    {
        var rows = new LegendDisplayRows();
        rows.Add(line);
        return rows;
    }

    static LegendDisplayRows CreateRows(string text)
    {
        var rows = new LegendDisplayRows();
        rows.Add(text);
        return rows;
    }
}

internal sealed class LegendTrainingCard(int commandId, int trainIndex)
{
    LegendDisplayLine title = LegendDisplayLine.Plain(commandId.ToString());

    public int CommandId { get; } = commandId;
    public int TrainIndex { get; } = trainIndex;
    public string Title
    {
        get => title.Text;
        set => title = LegendDisplayLine.Plain(value);
    }
    public LegendDisplayRows Rows { get; } = new();
    public bool Highlighted { get; set; }
    internal LegendDisplayLine StyledTitle
    {
        get => title;
        set => title = value;
    }

    public void AddRow(string row) => Rows.Add(row);
    public void AddRow(LegendDisplayLine row) => Rows.Add(row);
    public void AddRule() => Rows.Add(LegendDisplayLine.Rule);
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
    public LegendDisplayRows Rows { get; } = new();
    public bool Highlighted { get; set; }

    public void AddRow(string row) => Rows.Add(row);
    public void AddRow(LegendDisplayLine row) => Rows.Add(row);
    public void AddRule() => Rows.Add(LegendDisplayLine.Rule);
}

internal static class LegendColors
{
    public static LegendDisplayColor AccentColor(int which) => which switch
    {
        1 => LegendDisplayColor.Cyan,
        2 => LegendDisplayColor.Lime,
        3 => LegendDisplayColor.Red,
        _ => LegendDisplayColor.Yellow
    };

    public static LegendDisplayColor AccentColor(LegendBuffColor color)
        => AccentColor((int)color + 1);

    public static LegendDisplayColor BorderColor(LegendBuffColor color) => color switch
    {
        LegendBuffColor.Blue => LegendDisplayColor.Blue,
        LegendBuffColor.Green => LegendDisplayColor.Green,
        LegendBuffColor.Red => LegendDisplayColor.Red,
        _ => LegendDisplayColor.Yellow
    };

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
