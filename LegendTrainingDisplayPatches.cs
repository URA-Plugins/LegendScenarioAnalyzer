namespace LegendScenarioAnalyzer;

public sealed class LegendTrainingDisplayPatch
{
    readonly List<ILegendTrainingDisplayPatchOperation> operations = [];

    public LegendTrainingDisplayPatch()
    {
        Important = new(operations, LegendDisplayRowsTarget.Important);
        Extra = new(operations, LegendDisplayRowsTarget.Extra);
    }

    public LegendDisplayRowsPatch Important { get; }

    public LegendDisplayRowsPatch Extra { get; }

    public LegendTrainingCardPatch Training(LegendTrain train)
        => new(operations, new TrainingByTrainIndex((int)train));

    public LegendTrainingCardPatch TrainingByCommandId(int commandId)
        => new(operations, new TrainingByCommandId(commandId));

    public LegendSelectionCardPatch SelectionByBuffId(int buffId)
        => new(operations, new SelectionByBuffId(buffId));

    public LegendSelectionCardPatch Selection(LegendBuffColor color, int ordinalWithinColor)
        => new(operations, new SelectionByColorOrdinal(color, ordinalWithinColor));

    public LegendScenarioPanelPatch Scenario(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(operations, key);
    }

    internal void Apply(LegendTrainingDisplayEditor display)
    {
        foreach (var operation in operations)
            operation.Apply(display.Builder);
    }
}

public sealed class LegendTrainingCardPatch
{
    readonly List<ILegendTrainingDisplayPatchOperation> operations;
    readonly ILegendTrainingCardSelector selector;

    internal LegendTrainingCardPatch(
        List<ILegendTrainingDisplayPatchOperation> operations,
        ILegendTrainingCardSelector selector)
    {
        this.operations = operations;
        this.selector = selector;
    }

    public LegendTrainingCardPatch AddDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new TrainingCardAddRow(selector, text));
        return this;
    }

    public LegendTrainingCardPatch AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new TrainingCardAddRow(selector, text));
        return this;
    }

    public LegendTrainingCardPatch Highlight()
    {
        operations.Add(new TrainingCardHighlight(selector));
        return this;
    }

    public LegendTrainingCardPatch Title(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        operations.Add(new TrainingCardSetTitle(selector, title));
        return this;
    }
}

public sealed class LegendSelectionCardPatch
{
    readonly List<ILegendTrainingDisplayPatchOperation> operations;
    readonly ILegendSelectionCardSelector selector;

    internal LegendSelectionCardPatch(
        List<ILegendTrainingDisplayPatchOperation> operations,
        ILegendSelectionCardSelector selector)
    {
        this.operations = operations;
        this.selector = selector;
    }

    public LegendSelectionCardPatch AddDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new SelectionCardAddRow(selector, text));
        return this;
    }

    public LegendSelectionCardPatch AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new SelectionCardAddRow(selector, text));
        return this;
    }

    public LegendSelectionCardPatch Highlight()
    {
        operations.Add(new SelectionCardHighlight(selector));
        return this;
    }

    public LegendSelectionCardPatch Title(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        operations.Add(new SelectionCardSetTitle(selector, title));
        return this;
    }
}

public sealed class LegendDisplayRowsPatch
{
    readonly List<ILegendTrainingDisplayPatchOperation> operations;
    readonly LegendDisplayRowsTarget target;

    internal LegendDisplayRowsPatch(
        List<ILegendTrainingDisplayPatchOperation> operations,
        LegendDisplayRowsTarget target)
    {
        this.operations = operations;
        this.target = target;
    }

    public LegendDisplayRowsPatch AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new RowsAddText(target, text));
        return this;
    }
}

public sealed class LegendScenarioPanelPatch
{
    readonly List<ILegendTrainingDisplayPatchOperation> operations;
    readonly string key;

    internal LegendScenarioPanelPatch(
        List<ILegendTrainingDisplayPatchOperation> operations,
        string key)
    {
        this.operations = operations;
        this.key = key;
    }

    public LegendScenarioPanelPatch AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new ScenarioPanelAddRow(key, text));
        return this;
    }

    public LegendScenarioPanelPatch Title(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        operations.Add(new ScenarioPanelSetTitle(key, title));
        return this;
    }
}

interface ILegendTrainingDisplayPatchOperation
{
    void Apply(LegendTrainingDisplayBuilder builder);
}

interface ILegendTrainingCardSelector
{
    LegendTrainingCard Select(LegendTrainingDisplayBuilder builder);
}

interface ILegendSelectionCardSelector
{
    LegendSelectionCard Select(LegendTrainingDisplayBuilder builder);
}

sealed record TrainingByTrainIndex(int TrainIndex) : ILegendTrainingCardSelector
{
    public LegendTrainingCard Select(LegendTrainingDisplayBuilder builder)
        => builder.FindTrainingCardByTrainIndex(TrainIndex)
            ?? throw new InvalidOperationException($"传奇杯训练卡不存在: trainIndex={TrainIndex}");
}

sealed record TrainingByCommandId(int CommandId) : ILegendTrainingCardSelector
{
    public LegendTrainingCard Select(LegendTrainingDisplayBuilder builder)
        => builder.FindTrainingCardByCommandId(CommandId)
            ?? throw new InvalidOperationException($"传奇杯训练卡不存在: commandId={CommandId}");
}

sealed record SelectionByBuffId(int BuffId) : ILegendSelectionCardSelector
{
    public LegendSelectionCard Select(LegendTrainingDisplayBuilder builder)
        => builder.FindSelectionCardByBuffId(BuffId)
            ?? throw new InvalidOperationException($"传奇杯心得选择卡不存在: buffId={BuffId}");
}

sealed record SelectionByColorOrdinal(LegendBuffColor Color, int OrdinalWithinColor) : ILegendSelectionCardSelector
{
    public LegendSelectionCard Select(LegendTrainingDisplayBuilder builder)
        => builder.FindSelectionCard(Color, OrdinalWithinColor)
            ?? throw new InvalidOperationException($"传奇杯心得选择卡不存在: color={Color}, ordinal={OrdinalWithinColor}");
}

sealed record TrainingCardAddRow(ILegendTrainingCardSelector Selector, string Row)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
        => Selector.Select(builder).AddRow(Row);
}

sealed record TrainingCardHighlight(ILegendTrainingCardSelector Selector)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
        => Selector.Select(builder).Highlighted = true;
}

sealed record TrainingCardSetTitle(ILegendTrainingCardSelector Selector, string Title)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
        => Selector.Select(builder).Title = Title;
}

sealed record SelectionCardAddRow(ILegendSelectionCardSelector Selector, string Row)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
        => Selector.Select(builder).AddRow(Row);
}

sealed record SelectionCardHighlight(ILegendSelectionCardSelector Selector)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
        => Selector.Select(builder).Highlighted = true;
}

sealed record SelectionCardSetTitle(ILegendSelectionCardSelector Selector, string Title)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
        => Selector.Select(builder).Title = Title;
}

enum LegendDisplayRowsTarget
{
    Important,
    Extra
}

sealed record RowsAddText(LegendDisplayRowsTarget Target, string Row)
    : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
    {
        if (Target is LegendDisplayRowsTarget.Important)
            builder.ImportantRows.Add(Row);
        else
            builder.ExtraRows.Add(Row);
    }
}

sealed record ScenarioPanelAddRow(string Key, string Row) : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
    {
        var panel = builder.FindScenarioPanel(Key)
            ?? throw new InvalidOperationException($"传奇杯剧本面板不存在: key={Key}");

        panel.Content = $"{panel.Content}{Environment.NewLine}{Row}";
    }
}

sealed record ScenarioPanelSetTitle(string Key, string Title) : ILegendTrainingDisplayPatchOperation
{
    public void Apply(LegendTrainingDisplayBuilder builder)
    {
        var panel = builder.FindScenarioPanel(Key)
            ?? throw new InvalidOperationException($"传奇杯剧本面板不存在: key={Key}");

        panel.Title = Title;
        panel.ShowHeader = true;
    }
}
