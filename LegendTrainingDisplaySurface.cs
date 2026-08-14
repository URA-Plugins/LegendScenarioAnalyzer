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
    static readonly List<ModifierRegistration> Modifiers = [];
    static CurrentDisplay? currentDisplay;

    public static IDisposable RegisterModifier(
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        var registration = new ModifierRegistration(modifier);
        lock (CurrentGate)
            Modifiers.Add(registration);

        try
        {
            RefreshCurrent();
            return registration;
        }
        catch
        {
            registration.Remove(refresh: false);
            throw;
        }
    }

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

        return RenderCurrent(current, modifier, switchToWorkspace, cancellationToken);
    }

    public static bool RefreshCurrent(
        bool switchToWorkspace = false,
        CancellationToken cancellationToken = default)
    {
        CurrentDisplay? current;
        lock (CurrentGate)
            current = currentDisplay;

        return current is not null &&
            RenderCurrent(current, modifier: null, switchToWorkspace, cancellationToken);
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
        CurrentDisplay? previous;
        lock (CurrentGate)
        {
            previous = currentDisplay;
            currentDisplay = current;
        }

        try
        {
            _ = RenderCurrent(current, modifier: null, switchToWorkspace: true, CancellationToken.None);
        }
        catch
        {
            lock (CurrentGate)
                if (ReferenceEquals(currentDisplay, current))
                    currentDisplay = previous;
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

    static bool RenderCurrent(
        CurrentDisplay current,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>? modifier,
        bool switchToWorkspace,
        CancellationToken cancellationToken)
    {
        ModifierRegistration[] modifiers;
        lock (CurrentGate)
            modifiers = [.. Modifiers];

        return current.Render(
            (context, editor) =>
            {
                foreach (var registration in modifiers)
                    registration.Apply(context, editor);
                modifier?.Invoke(context, editor);
            },
            switchToWorkspace,
            () => !cancellationToken.IsCancellationRequested && IsCurrent(current));
    }

    internal static LegendDisplayLine CreateStyledLine(LegendDisplaySegment[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Any(segment => segment.Text is null))
            throw new ArgumentException("显示片段文本不能为 null。", nameof(segments));
        return LegendDisplayLine.Styled(segments);
    }

    sealed record CurrentDisplay(
        object Owner,
        Func<
            Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>?,
            bool,
            Func<bool>,
            bool> Render);

    sealed class ModifierRegistration(
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier) : IDisposable
    {
        int disposed;

        internal void Apply(
            LegendTrainingDisplayContext context,
            LegendTrainingDisplayEditor editor)
            => modifier(context, editor);

        public void Dispose() => Remove(refresh: true);

        internal void Remove(bool refresh)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            lock (CurrentGate)
                Modifiers.Remove(this);
            if (refresh)
                RefreshCurrent();
        }
    }
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

    public void AddStyled(params LegendDisplaySegment[] segments)
        => card.AddRow(LegendTrainingDisplay.CreateStyledLine(segments));
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

    public void AddStyled(params LegendDisplaySegment[] segments)
        => card.AddRow(LegendTrainingDisplay.CreateStyledLine(segments));
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

    public void AddStyled(params LegendDisplaySegment[] segments)
        => rows.Add(LegendTrainingDisplay.CreateStyledLine(segments));
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

    public void AddStyled(params LegendDisplaySegment[] segments)
        => panel.AddRow(LegendTrainingDisplay.CreateStyledLine(segments));
}
