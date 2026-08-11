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

    public static bool ModifyCurrent(
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier,
        bool switchToWorkspace = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        CurrentDisplay? current;
        lock (CurrentGate)
            current = currentDisplay;

        if (current is null)
            return false;

        return current.Render(
            modifier,
            switchToWorkspace,
            () => !cancellationToken.IsCancellationRequested && IsCurrent(current));
    }

    internal static void SetCurrentDisplay(
        object owner,
        Func<
            Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>?,
            bool,
            Func<bool>,
            bool> render)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(render);

        var current = new CurrentDisplay(owner, render);
        lock (CurrentGate)
            currentDisplay = current;

        try
        {
            _ = current.Render(null, true, () => IsCurrent(current));
        }
        catch
        {
            lock (CurrentGate)
                if (ReferenceEquals(currentDisplay, current))
                    currentDisplay = null;
            throw;
        }
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

    static bool IsCurrent(CurrentDisplay candidate)
    {
        lock (CurrentGate)
            return ReferenceEquals(currentDisplay, candidate);
    }

    sealed record CurrentDisplay(
        object Owner,
        Func<
            Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>?,
            bool,
            Func<bool>,
            bool> Render);
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

    public bool Highlighted
    {
        get => card.Highlighted;
        set => card.Highlighted = value;
    }

    public void SetTitle(string title) => Title = title;

    public void Highlight() => Highlighted = true;

    public void AddDescription(string text) => AddText(text);

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(text);
    }

    public void AddRow(string row)
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

    public bool Highlighted
    {
        get => card.Highlighted;
        set => card.Highlighted = value;
    }

    public void SetTitle(string title) => Title = title;

    public void Highlight() => Highlighted = true;

    public void AddDescription(string text) => AddText(text);

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(text);
    }

    public void AddRow(string row)
    {
        ArgumentNullException.ThrowIfNull(row);
        card.AddRow(row);
    }
}

public sealed class LegendDisplayRowsEditor
{
    readonly LegendDisplayRows rows;

    internal LegendDisplayRowsEditor(LegendDisplayRows rows)
    {
        this.rows = rows;
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(text);
    }

    public void AddRow(string row)
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

        var panel = new LegendDisplayPanel(key, title, string.Empty, showHeader: true);
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
        panel.Content = text;
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(text);
    }

    public void AddRow(string row)
    {
        ArgumentNullException.ThrowIfNull(row);
        panel.AddRow(row);
    }
}
