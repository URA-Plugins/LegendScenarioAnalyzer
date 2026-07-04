using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace LegendScenarioAnalyzer;

internal sealed class LegendTrainingDisplayBuilder
{
    public LegendScenarioStage Stage { get; private init; }
    public List<LegendDisplayPanel> HeaderPanels { get; } = [];
    public List<LegendDisplayPanel> ScenarioPanels { get; } = [];
    public List<IRenderable> ImportantRows { get; } = [];
    public List<LegendTrainingCard> TrainingCards { get; } = [];
    public List<LegendSelectionCard> SelectionCards { get; } = [];
    public List<IRenderable> ExtraRows { get; } = [];

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

        builder.HeaderPanels.Add(new("date", "日期", new Text($"{turn.Year}{LegendDisplayText.Year} {turn.Month}{LegendDisplayText.Month}{turn.HalfMonth}"), ratio: 4));
        builder.HeaderPanels.Add(new("total", "总属性", new Markup($"[cyan]总属性: {totalValue}, Pt: {data.CharaInfo.skill_point}[/]"), ratio: 6));
        builder.HeaderPanels.Add(new("vital", "体力", new Markup($"{LegendDisplayText.Vital}: [green]{turn.Vital}[/]/{turn.MaxVital}"), ratio: 6));
        builder.HeaderPanels.Add(new("motivation", "干劲", new Markup(LegendDisplayText.MotivationMarkup(data.CharaInfo.motivation)), ratio: 3));

        AddImportantRows(context, builder);
        AddScenarioPanels(context, builder);

        if (data.Stage == LegendScenarioStage.Training)
            AddTrainingCards(context, builder);
        else
            AddNonTrainingRows(context, builder);

        return builder;
    }

    static void AddImportantRows(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder)
    {
        var turn = context.Turn;
        var data = context.ResponseData;

        if (context.PreviousTurn != turn.Turn - 1
            && context.PreviousTurn != turn.Turn
            && turn.Turn != 1)
        {
            builder.ImportantRows.Add(new Markup(LegendDisplayText.WrongTurnAlert(context.PreviousTurn, turn.Turn)));
        }

        var availableTrainingCount = data.HomeInfo!.command_info_array.Count(x => x.is_enable == 1);
        if (availableTrainingCount <= 1)
            builder.ImportantRows.Add(new Markup($"[aqua]非训练回合 playingState = {data.CharaInfo.playing_state}[/]"));

        if (data.Stage != LegendScenarioStage.Training)
            builder.ImportantRows.Add(new Markup($"[aqua]非训练阶段: {LegendDisplayText.StageName(data.Stage)}[/]"));

        if (data.CharaInfo.skill_point > 9500)
            builder.ImportantRows.Add(new Markup("[red]剩余PT>9500（上限9999），请及时学习技能[/]"));
    }

    static void AddScenarioPanels(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder)
    {
        var turn = context.Turn;
        var dataSet = context.DataSet;
        var buffPeriod = (turn.Turn - 1) % 6 + 1;
        var buffPeriodColor = buffPeriod switch
        {
            <= 3 => "white",
            4 => "yellow",
            _ => "red"
        };

        builder.ScenarioPanels.Add(new(
            "buff-period",
            "心得周期",
            new Markup($"心得回合周期 [{buffPeriodColor}]{buffPeriod}[/]/6"),
            ratio: 3,
            showHeader: true));

        var gaugeCounts = turn.GaugeCounts;
        builder.ScenarioPanels.Add(new(
            "gauge-level",
            "心得等级",
            new Markup($"[cyan]{gaugeCounts[9046]}/8[/] [#00ff00]{gaugeCounts[9047]}/8[/] [#ff8080]{gaugeCounts[9048]}/8[/]"),
            ratio: 3,
            showHeader: true));

        var colorInfo = CreateBuffColorInfo(turn, dataSet);
        builder.ScenarioPanels.Add(new(
            "buff-color",
            "心得颜色",
            colorInfo,
            ratio: 6,
            showHeader: true));
    }

    static IRenderable CreateBuffColorInfo(TurnInfoLegend turn, Gallop.SingleModeLegendDataSet dataSet)
    {
        if (turn.Turn > 36 && dataSet.masterly_bonus_info is not null)
        {
            var mainColor =
                dataSet.masterly_bonus_info.info_9046 is not null ? 1 :
                dataSet.masterly_bonus_info.info_9047 is not null ? 2 :
                dataSet.masterly_bonus_info.info_9048 is not null ? 3 : 0;
            return new Markup($"{LegendColors.MarkupPrefix(mainColor)}主色：{LegendColors.Name(mainColor)}[/]");
        }

        var blueBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 1);
        var greenBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 2);
        var redBuffCount = dataSet.buff_info_array.Count(x => x.buff_id / 1000 == 3);
        return new Markup($"当前心得颜色：[cyan]蓝{blueBuffCount}[/] [#00ff00]绿{greenBuffCount}[/] [#ff8080]红{redBuffCount}[/]");
    }

    static void AddTrainingCards(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder)
    {
        var maxScore = context.TrainStats.Count == 0 ? 0 : context.TrainStats.Max(x => x.FiveValueGain.Sum());
        foreach (var command in context.Turn.CommandInfoArray)
        {
            var stats = context.TrainStats[command.TrainIndex - 1];
            builder.TrainingCards.Add(CreateTrainingCard(context.Turn, command, stats, maxScore));
        }

        if (context.ResponseData.CharaInfo.chara_effect_id_array.Any(x => x == 104))
            builder.ExtraRows.Add(new Text("团卡彩圈生效中"));
    }

    static LegendTrainingCard CreateTrainingCard(
        TurnInfoLegend turn,
        LegendCommandInfo command,
        TrainStats stats,
        int maxScore)
    {
        var failureRate = stats.FailureRate switch
        {
            >= 40 => $"[red]({stats.FailureRate}%)[/]",
            >= 20 => $"[darkorange]({stats.FailureRate}%)[/]",
            > 0 => $"[yellow]({stats.FailureRate}%)[/]",
            _ => string.Empty
        };
        var card = new LegendTrainingCard(command.CommandId, command.TrainIndex)
        {
            Title = $"{LegendDisplayText.TrainName(command.TrainIndex)}{failureRate}"
        };

        var currentStat = turn.StatsRevised[command.TrainIndex - 1];
        var statUpToMax = turn.MaxStatsRevised[command.TrainIndex - 1] - currentStat;
        card.AddRow(new Text(LegendDisplayText.CurrentRemainStat));
        card.AddRow(new Markup($"{currentStat}:{statUpToMax switch
        {
            > 400 => $"{statUpToMax}",
            > 200 => $"[yellow]{statUpToMax}[/]",
            _ => $"[red]{statUpToMax}[/]"
        }}"));
        card.AddRule();

        var afterVital = stats.VitalGain + turn.Vital;
        card.AddRow(new Markup(afterVital switch
        {
            < 30 => $"{LegendDisplayText.Vital}:[red]{afterVital}[/]/{turn.MaxVital}",
            < 50 => $"{LegendDisplayText.Vital}:[darkorange]{afterVital}[/]/{turn.MaxVital}",
            < 70 => $"{LegendDisplayText.Vital}:[yellow]{afterVital}[/]/{turn.MaxVital}",
            _ => $"{LegendDisplayText.Vital}:[green]{afterVital}[/]/{turn.MaxVital}"
        }));

        var gaugeGain = command.GaugeGain;
        var gaugeId = gaugeGain.LegendId - 9045;
        var currentGauge = turn.GaugeCounts[gaugeGain.LegendId];
        var gaugeText = $"{LegendColors.MarkupPrefix(gaugeId)}{LegendColors.Name(gaugeId)} {currentGauge}+{gaugeGain.GainGauge}[/]";
        card.AddRow(new Markup($"Lv{command.TrainLevel} | {gaugeText}"));
        card.AddRule();

        var score = stats.FiveValueGain.Sum();
        card.AddRow(new Markup(score == maxScore
            ? $"{LegendDisplayText.StatSimple}:[aqua]{score}[/]|Pt:{stats.PtGain}"
            : $"{LegendDisplayText.StatSimple}:{score}|Pt:{stats.PtGain}"));

        foreach (var trainingPartner in command.TrainingPartners)
        {
            card.AddRow(new Markup(trainingPartner.Name));
            if (trainingPartner.Shining)
                card.BorderColor = Color.LightGreen;
        }

        for (var i = 5 - command.TrainingPartners.Count; i > 0; i--)
            card.AddRow(new Text(string.Empty));
        card.AddRule();

        return card;
    }

    static void AddNonTrainingRows(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder)
    {
        builder.ExtraRows.Add(new Text($"非训练阶段: {LegendDisplayText.StageName(context.ResponseData.Stage)}"));
        if (context.ResponseData.Stage != LegendScenarioStage.BuffSelection)
            return;

        AddSelectionCards(context, builder);
    }

    static void AddSelectionCards(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder)
    {
        GameGlobal.LoadLegendBuffs();
        var buffInfoList = context.DataSet.obtainable_buff_id_array
            .Select(RequireLegendBuff)
            .OrderBy(x => x.color)
            .ThenByDescending(x => x.rank)
            .ThenBy(x => x.buffId)
            .ToArray();

        var ordinalsByColor = new Dictionary<LegendBuffColor, int>();
        foreach (var buff in buffInfoList)
        {
            var color = ToBuffColor(buff.color);
            var ordinal = ordinalsByColor.GetValueOrDefault(color) + 1;
            ordinalsByColor[color] = ordinal;
            builder.SelectionCards.Add(CreateSelectionCard(buff, color, ordinal));
        }
    }

    static LegendSelectionCard CreateSelectionCard(LegendBuff buff, LegendBuffColor color, int ordinalWithinColor)
    {
        return new(
            buff.buffId,
            color,
            ordinalWithinColor,
            buff.name,
            buff.cn_effect,
            buff.rank);
    }

    static LegendBuff RequireLegendBuff(int buffId)
        => GameGlobal.LegendBuffInfo.FirstOrDefault(x => x.buffId == buffId)
            ?? throw new InvalidDataException($"legend_buff.csv 缺少 buffId={buffId} 的心得数据。");

    static LegendBuffColor ToBuffColor(int color)
        => color switch
        {
            0 => LegendBuffColor.Blue,
            1 => LegendBuffColor.Green,
            2 => LegendBuffColor.Red,
            _ => throw new InvalidDataException($"legend_buff.csv 包含未知心得颜色: color={color}")
        };
}

internal static class LegendTrainingDisplayRenderer
{
    public static IRenderable Render(LegendTrainingDisplayBuilder builder)
    {
        var layout = new Layout().SplitColumns(
            new Layout("Main").Size(CommandInfoLayout.Current.MainSectionWidth).SplitRows(
                BuildPanelRow("体力干劲条", builder.HeaderPanels, showHeaders: false).Size(3),
                new Layout("重要信息").Size(5),
                BuildPanelRow("剧本信息", builder.ScenarioPanels, showHeaders: true).Size(3),
                new Layout("训练信息")).Ratio(4),
            new Layout("Ext").Ratio(1));

        layout["重要信息"].Update(new Panel(BuildRows(builder.ImportantRows)).Expand());
        layout["训练信息"].Update(BuildMainArea(builder));
        layout["Ext"].Update(BuildExtraTable(builder.ExtraRows));
        return layout;
    }

    static Layout BuildPanelRow(
        string name,
        IReadOnlyList<LegendDisplayPanel> panels,
        bool showHeaders)
    {
        var row = new Layout(name);
        var children = panels.Count == 0
            ? [new Layout("empty").Ratio(1)]
            : panels.Select(x => new Layout(x.Key).Ratio(x.Ratio)).ToArray();
        row.SplitColumns(children);

        foreach (var panel in panels)
        {
            var panelView = new Panel(panel.Content).Expand();
            if (showHeaders && panel.ShowHeader)
                panelView.Header(panel.Title);
            row[panel.Key].Update(panelView);
        }

        return row;
    }

    static IRenderable BuildRows(IReadOnlyList<IRenderable> rows)
    {
        if (rows.Count == 0)
            return new Text(string.Empty);

        var table = new Table();
        table.HideHeaders();
        table.NoBorder();
        table.AddColumn(string.Empty);
        foreach (var row in rows)
            table.AddRow(row);
        return table;
    }

    static IRenderable BuildMainArea(LegendTrainingDisplayBuilder builder)
    {
        if (builder.TrainingCards.Count != 0)
            return BuildTrainingGrid(builder.TrainingCards);
        if (builder.SelectionCards.Count != 0)
            return BuildSelectionList(builder.SelectionCards);
        return new Text("非训练阶段");
    }

    static IRenderable BuildTrainingGrid(IReadOnlyList<LegendTrainingCard> cards)
    {
        if (cards.Count == 0)
            return new Text("非训练阶段");

        var grid = new Grid();
        grid.AddColumns(Math.Max(6, cards.Count));
        foreach (var column in grid.Columns)
            column.Padding = new Padding(0, 0, 0, 0);

        grid.AddRow([.. cards.Select(x => new Padder(BuildTrainingTable(x)).Padding(0, 0, 0, 0))]);
        return grid;
    }

    static Table BuildTrainingTable(LegendTrainingCard card)
    {
        var table = new Table().AddColumn(card.Title);
        foreach (var row in card.Rows)
            table.AddRow(row);

        if (card.BorderColor is { } borderColor)
            table.BorderColor(borderColor);

        return table;
    }

    static IRenderable BuildSelectionList(IReadOnlyList<LegendSelectionCard> cards)
    {
        var table = new Table();
        table.HideHeaders();
        table.NoBorder();
        table.AddColumn(string.Empty);
        foreach (var card in cards)
            table.AddRow(new Padder(BuildSelectionPanel(card)).Padding(0, 0, 0, 0));

        return table;
    }

    static Panel BuildSelectionPanel(LegendSelectionCard card)
    {
        var panel = new Panel(new Markup(BuildSelectionLine(card)));
        panel.BorderColor(card.BorderColor ?? LegendColors.BorderColor(card.Color));
        return panel;
    }

    static string BuildSelectionLine(LegendSelectionCard card)
    {
        var color = LegendColors.MarkupPrefix(card.Color);
        var icon = TruncateDisplay($"● ★{card.Rank}", 6);
        var label = TruncateDisplay(card.SelectionLabel, 13);
        var title = TruncateDisplay(card.Title, 12);
        var effect = TruncateDisplay(card.Effect, 12);
        var extra = TruncateDisplay(
            string.Join(
                " | ",
                card.Rows
                    .Select(RenderInlineText)
                    .Select(x => CompactSelectionInlineText(card, x))
                    .Where(x => x.Length != 0)),
            38);
        var extraText = extra.Length == 0 ? string.Empty : $"  [grey]{Markup.Escape(extra)}[/]";

        return $"{color}{Markup.Escape(icon)} {Markup.Escape(label)}[/]  [bold]{Markup.Escape(title)}[/]  {Markup.Escape(effect)}{extraText}  {color}▶[/]";
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

        var nameStart = value.IndexOf('（');
        var nameEnd = nameStart < 0 ? -1 : value.IndexOf('）', nameStart + 1);
        if (nameStart >= 0 && nameEnd > nameStart)
        {
            var name = value[(nameStart + 1)..nameEnd].Trim();
            var suffix = value[(nameEnd + 1)..].Trim();
            value = suffix.Length == 0 ? name : $"{name} {suffix}";
        }

        return $"AI建议:{value}";
    }

    static string TruncateDisplay(string text, int maxWidth)
    {
        if (DisplayWidth(text) <= maxWidth)
            return text;

        var builder = new StringBuilder();
        var width = 0;
        foreach (var ch in text)
        {
            var nextWidth = DisplayWidth(ch);
            if (width + nextWidth > maxWidth - 1)
                break;

            builder.Append(ch);
            width += nextWidth;
        }

        return builder.Append('…').ToString();
    }

    static int DisplayWidth(string text)
    {
        var width = 0;
        foreach (var ch in text)
            width += DisplayWidth(ch);
        return width;
    }

    static int DisplayWidth(char ch) => ch <= 0x7f ? 1 : 2;

    static string RenderInlineText(IRenderable row)
    {
        if (row is Rule)
            return string.Empty;

        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new InlineConsoleOutput(writer),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Write(row);
        return NormalizeInlineText(writer.ToString());
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

    static IRenderable BuildExtraTable(IReadOnlyList<IRenderable> rows)
    {
        var table = new Table().AddColumn("Extras");
        table.HideHeaders();
        foreach (var row in rows)
            table.AddRow(row);
        return table;
    }
}

sealed class InlineConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
{
    public TextWriter Writer { get; } = writer;
    public bool IsTerminal => false;
    public int Width => 120;
    public int Height => 1;

    public void SetEncoding(Encoding encoding)
    {
    }
}

internal sealed class LegendDisplayPanel(
    string key,
    string title,
    IRenderable content,
    int ratio = 1,
    bool showHeader = false)
{
    public string Key { get; } = key;
    public string Title { get; set; } = title;
    public IRenderable Content { get; set; } = content;
    public int Ratio { get; set; } = ratio;
    public bool ShowHeader { get; set; } = showHeader;
}

internal sealed class LegendTrainingCard(int commandId, int trainIndex)
{
    public int CommandId { get; } = commandId;
    public int TrainIndex { get; } = trainIndex;
    public string Title { get; set; } = commandId.ToString();
    public List<IRenderable> Rows { get; } = [];
    public Color? BorderColor { get; set; }

    public void AddRow(IRenderable row) => Rows.Add(row);

    public void AddRule() => Rows.Add(new Rule());
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
    public List<IRenderable> Rows { get; } = [];
    public Color? BorderColor { get; set; }

    public void AddRow(IRenderable row) => Rows.Add(row);

    public void AddRule() => Rows.Add(new Rule());
}

internal static class LegendColors
{
    public static string MarkupPrefix(int which) => which switch
    {
        1 => "[cyan]",
        2 => "[#00ff00]",
        3 => "[#ff8080]",
        _ => "[#ffff00]"
    };

    public static string MarkupPrefix(LegendBuffColor color)
        => MarkupPrefix((int)color + 1);

    public static Color BorderColor(LegendBuffColor color) => color switch
    {
        LegendBuffColor.Blue => Color.Blue,
        LegendBuffColor.Green => Color.Green,
        LegendBuffColor.Red => Color.Red,
        _ => Color.Yellow
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
