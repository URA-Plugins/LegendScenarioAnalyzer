using Spectre.Console;
using Spectre.Console.Rendering;

namespace LegendScenarioAnalyzer;

public enum LegendTrain
{
    Speed = 1,
    Stamina = 2,
    Power = 3,
    Guts = 4,
    Wiz = 5,
    Wisdom = 5
}

public enum LegendBuffColor
{
    Blue = 0,
    Green = 1,
    Red = 2
}

public static class LegendTrainingDisplay
{
    static readonly object CurrentGate = new();
    static CurrentDisplay? currentDisplay;

    public static IDisposable Modify(
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier,
        int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        return LegendTrainingDisplayRegistry.Register(modifier, priority);
    }

    public static IDisposable Patch(Action<LegendTrainingDisplayPatch> patch, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var displayPatch = new LegendTrainingDisplayPatch();
        patch(displayPatch);
        return Modify((_, display) => displayPatch.Apply(display), priority);
    }

    public static bool ModifyCurrent(
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        CurrentDisplay? current;
        lock (CurrentGate)
            current = currentDisplay;

        if (current is null)
            return false;

        current.Render(modifier);
        return true;
    }

    public static bool PatchCurrent(Action<LegendTrainingDisplayPatch> patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var displayPatch = new LegendTrainingDisplayPatch();
        patch(displayPatch);
        return ModifyCurrent((_, display) => displayPatch.Apply(display));
    }

    internal static void SetCurrentDisplay(
        object owner,
        Action<Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>?> render)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(render);

        lock (CurrentGate)
            currentDisplay = new(owner, render);
    }

    internal static void ClearCurrentDisplay(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        lock (CurrentGate)
        {
            if (ReferenceEquals(currentDisplay?.Owner, owner))
                currentDisplay = null;
        }
    }

    sealed record CurrentDisplay(
        object Owner,
        Action<Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>?> Render);
}

public sealed class LegendTrainingDisplayEditor
{
    readonly LegendTrainingDisplayBuilder builder;

    internal LegendTrainingDisplayEditor(LegendTrainingDisplayBuilder builder)
    {
        this.builder = builder;
        Training = new(builder);
        Important = new(builder.ImportantRows);
        Extra = new(builder.ExtraRows);
        Scenario = new(builder);
        Selection = new(builder);
    }

    public LegendTrainingCardsEditor Training { get; }
    public LegendDisplayRowsEditor Important { get; }
    public LegendDisplayRowsEditor Extra { get; }
    public LegendScenarioPanelsEditor Scenario { get; }
    public LegendSelectionCardsEditor Selection { get; }

    internal LegendTrainingDisplayBuilder Builder => builder;
}

public sealed class LegendTrainingCardsEditor
{
    readonly LegendTrainingDisplayBuilder builder;

    internal LegendTrainingCardsEditor(LegendTrainingDisplayBuilder builder)
    {
        this.builder = builder;
    }

    public LegendTrainingCardEditor Get(LegendTrain train)
    {
        var card = builder.FindTrainingCardByTrainIndex((int)train)
            ?? throw new InvalidOperationException($"传奇杯训练卡不存在: {train}");
        return new(card);
    }

    public LegendTrainingCardEditor GetByCommandId(int commandId)
    {
        var card = builder.FindTrainingCardByCommandId(commandId)
            ?? throw new InvalidOperationException($"传奇杯训练 command 不存在: {commandId}");
        return new(card);
    }

    public void Modify(LegendTrain train, Action<LegendTrainingCardEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(Get(train));
    }

    public void ModifyByCommandId(int commandId, Action<LegendTrainingCardEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(GetByCommandId(commandId));
    }
}

public sealed class LegendTrainingCardEditor
{
    readonly LegendTrainingCard card;

    internal LegendTrainingCardEditor(LegendTrainingCard card)
    {
        this.card = card;
    }

    public int CommandId => card.CommandId;
    public LegendTrain Train => (LegendTrain)card.TrainIndex;

    public string Title
    {
        get => card.Title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            card.Title = value;
        }
    }

    public Color? BorderColor
    {
        get => card.BorderColor;
        set => card.BorderColor = value;
    }

    public void SetTitle(string title) => Title = title;

    public void SetBorder(Color color) => BorderColor = color;

    public void AddDescription(string text) => AddText(text);

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        card.AddRow(row);
    }
}

public sealed class LegendSelectionCardsEditor
{
    readonly LegendTrainingDisplayBuilder builder;

    internal LegendSelectionCardsEditor(LegendTrainingDisplayBuilder builder)
    {
        this.builder = builder;
    }

    public LegendSelectionCardEditor GetByBuffId(int buffId)
    {
        var card = builder.FindSelectionCardByBuffId(buffId)
            ?? throw new InvalidOperationException($"传奇杯心得选择卡不存在: buffId={buffId}");
        return new(card);
    }

    public LegendSelectionCardEditor Get(LegendBuffColor color, int ordinalWithinColor)
    {
        var card = builder.FindSelectionCard(color, ordinalWithinColor)
            ?? throw new InvalidOperationException($"传奇杯心得选择卡不存在: color={color}, ordinal={ordinalWithinColor}");
        return new(card);
    }

    public void ModifyByBuffId(int buffId, Action<LegendSelectionCardEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(GetByBuffId(buffId));
    }

    public void Modify(LegendBuffColor color, int ordinalWithinColor, Action<LegendSelectionCardEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(Get(color, ordinalWithinColor));
    }
}

public sealed class LegendSelectionCardEditor
{
    readonly LegendSelectionCard card;

    internal LegendSelectionCardEditor(LegendSelectionCard card)
    {
        this.card = card;
    }

    public int BuffId => card.BuffId;
    public LegendBuffColor Color => card.Color;
    public int OrdinalWithinColor => card.OrdinalWithinColor;

    public string Title
    {
        get => card.Title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            card.Title = value;
        }
    }

    public Color? BorderColor
    {
        get => card.BorderColor;
        set => card.BorderColor = value;
    }

    public void SetTitle(string title) => Title = title;

    public void SetBorder(Color color) => BorderColor = color;

    public void AddDescription(string text) => AddText(text);

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        card.AddRow(row);
    }
}

public sealed class LegendDisplayRowsEditor
{
    readonly List<IRenderable> rows;

    internal LegendDisplayRowsEditor(List<IRenderable> rows)
    {
        this.rows = rows;
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        rows.Add(row);
    }
}

public sealed class LegendScenarioPanelsEditor
{
    readonly LegendTrainingDisplayBuilder builder;

    internal LegendScenarioPanelsEditor(LegendTrainingDisplayBuilder builder)
    {
        this.builder = builder;
    }

    public LegendDisplayPanelEditor Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var panel = builder.FindScenarioPanel(key)
            ?? throw new InvalidOperationException($"传奇杯剧本面板不存在: key={key}");
        return new(panel);
    }

    public LegendDisplayPanelEditor Add(string key, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(title);

        if (builder.FindScenarioPanel(key) is not null)
            throw new InvalidOperationException($"传奇杯剧本面板已存在: key={key}");

        var panel = new LegendDisplayPanel(key, title, new Text(string.Empty), showHeader: true);
        builder.ScenarioPanels.Add(panel);
        return new(panel);
    }

    public void Modify(string key, Action<LegendDisplayPanelEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(Get(key));
    }
}

public sealed class LegendDisplayPanelEditor
{
    readonly LegendDisplayPanel panel;

    internal LegendDisplayPanelEditor(LegendDisplayPanel panel)
    {
        this.panel = panel;
    }

    public string Key => panel.Key;

    public string Title
    {
        get => panel.Title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            panel.Title = value;
            panel.ShowHeader = true;
        }
    }

    public void SetTitle(string title) => Title = title;

    public void SetDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        panel.Content = new Text(text);
    }

    public void SetMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        panel.Content = new Markup(markup);
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        panel.Content = AppendRow(panel.Content, row);
    }

    static IRenderable AppendRow(IRenderable current, IRenderable row)
    {
        var table = new Table();
        table.HideHeaders();
        table.NoBorder();
        table.AddColumn(string.Empty);
        table.AddRow(current);
        table.AddRow(row);
        return table;
    }
}
