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

public readonly record struct LegendTrainingDisplayId(int SingleModeCharaId, int Turn);

public static class LegendTrainingDisplay
{
    static readonly object Gate = new();
    static readonly Dictionary<LegendTrainingDisplayId, DisplayUnit> Units = [];
    static long nextProducerSequence;

    public static LegendTrainingDisplayPartProducer RegisterPartProducer()
        => new(Interlocked.Increment(ref nextProducerSequence));

    internal static void Update(
        object owner,
        LegendTrainingDisplayId id,
        LegendTrainingDisplayContext context,
        Func<LegendTrainingDisplayContext, LegendTrainingDisplayBuilder> createBuilder,
        Action<LegendTrainingDisplayId, UmamusumeResponseAnalyzer.TerminalGui.WorkspaceContent, bool> publish)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(createBuilder);
        ArgumentNullException.ThrowIfNull(publish);

        lock (Gate)
        {
            if (!Units.TryGetValue(id, out var unit))
                Units.Add(id, unit = new());
            unit.Scenario = new(owner, context, createBuilder, publish);
        }
    }

    public static bool Show(
        LegendTrainingDisplayId id,
        bool switchToWorkspace = false,
        CancellationToken cancellationToken = default)
    {
        ScenarioPart scenario;
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>[] parts;
        lock (Gate)
        {
            if (!Units.TryGetValue(id, out var unit) || unit.Scenario is not { } value)
                return false;
            scenario = value;
            parts = [.. unit.Parts
                .OrderBy(entry => entry.Key.Sequence)
                .Select(entry => entry.Value)];
        }

        var builder = scenario.CreateBuilder(scenario.Context);
        var editor = new LegendTrainingDisplayEditor(builder);
        foreach (var part in parts)
            part(scenario.Context, editor);
        var content = LegendTrainingDisplayRenderer.Render(builder);
        if (cancellationToken.IsCancellationRequested)
            return false;
        scenario.Publish(id, content, switchToWorkspace);
        return true;
    }

    internal static void Remove(object owner, LegendTrainingDisplayId id)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (Gate)
            if (Units.TryGetValue(id, out var unit) && ReferenceEquals(unit.Scenario?.Owner, owner))
                Units.Remove(id);
    }

    internal static void Clear(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (Gate)
            foreach (var id in Units
                         .Where(entry => ReferenceEquals(entry.Value.Scenario?.Owner, owner))
                         .Select(entry => entry.Key)
                         .ToArray())
                Units.Remove(id);
    }

    internal static void UpdatePart(
        LegendTrainingDisplayPartProducer producer,
        LegendTrainingDisplayId id,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> part)
    {
        ArgumentNullException.ThrowIfNull(part);
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(producer.IsDisposed, producer);
            if (!Units.TryGetValue(id, out var unit))
                Units.Add(id, unit = new());
            unit.Parts[producer] = part;
        }
    }

    internal static void RemoveProducer(LegendTrainingDisplayPartProducer producer)
    {
        lock (Gate)
        {
            foreach (var (id, unit) in Units.ToArray())
            {
                unit.Parts.Remove(producer);
                if (unit.Scenario is null && unit.Parts.Count == 0)
                    Units.Remove(id);
            }
        }
    }

    internal static LegendDisplayLine CreateStyledLine(LegendDisplaySegment[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Any(segment => segment.Text is null))
            throw new ArgumentException("显示片段文本不能为 null。", nameof(segments));
        return LegendDisplayLine.Styled(segments);
    }

    sealed record ScenarioPart(
        object Owner,
        LegendTrainingDisplayContext Context,
        Func<LegendTrainingDisplayContext, LegendTrainingDisplayBuilder> CreateBuilder,
        Action<LegendTrainingDisplayId, UmamusumeResponseAnalyzer.TerminalGui.WorkspaceContent, bool> Publish);

    sealed class DisplayUnit
    {
        internal ScenarioPart? Scenario { get; set; }
        internal Dictionary<LegendTrainingDisplayPartProducer, Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>> Parts { get; } = [];
    }
}

public sealed class LegendTrainingDisplayPartProducer : IDisposable
{
    int disposed;

    internal LegendTrainingDisplayPartProducer(long sequence)
    {
        Sequence = sequence;
    }

    internal long Sequence { get; }
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public void Update(
        LegendTrainingDisplayId id,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> part)
        => LegendTrainingDisplay.UpdatePart(this, id, part);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            LegendTrainingDisplay.RemoveProducer(this);
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
