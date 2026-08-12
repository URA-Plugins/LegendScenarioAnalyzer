using Gallop;
using UmamusumeResponseAnalyzer.Plugin;
using UmamusumeResponseAnalyzer.TerminalGui;

namespace LegendScenarioAnalyzer;

public sealed class LegendScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "LegendScenarioAnalyzer";
    const string TrainingPanelKey = "training";

    readonly object renderGate = new();
    readonly LegendDisplayHistory history = new(WorkspaceTitle, TrainingPanelKey, "传奇杯训练");
    Workspace? workspace;
    int currentTurn;

    public void Initialize(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        history.Initialize(context.Application);
        context.Analyzers.Register<SingleModeLegendCheckEventResponse>(
            AnalyzerKind.Response,
            [
                EndpointPattern.Regex(
                    "/umamusume/single_mode_legend/(?:change_short_cut|check_event|cm_end|continue|exec_command|finish_claw_crane|gain_skills|legend_race_(?:continue|end|entry|out|start)|popularity_end|race_(?:end|entry|out))")
            ],
            invocation => Analyze(invocation.Payload),
            priority: 1);
        context.Analyzers.Register<SingleModeLegendLoadResponse>(
            AnalyzerKind.Response,
            [EndpointPattern.Exact("/umamusume/single_mode_legend/load")],
            invocation => Analyze(invocation.Payload),
            priority: 1);
    }

    public void Dispose()
    {
        history.Stop();
        LegendTrainingDisplay.ClearCurrentDisplay(this);

        lock (renderGate)
        {
            if (workspace is not { } target)
                return;

            target.RemovePanel(TrainingPanelKey);
            workspace = null;
        }
    }

    public Task ConfigPromptAsync(
        Terminal.Gui.App.IApplication application,
        CancellationToken cancellationToken = default)
        => history.ConfigPromptAsync(application, cancellationToken);

    public ValueTask Analyze(SingleModeLegendCheckEventResponse response)
    {
        if (response.data is not { } data)
            return ValueTask.CompletedTask;

        return AnalyzeLegendResponse(new(
            response,
            data.chara_info,
            data.home_info,
            data.legend_data_set,
            data.unchecked_event_array,
            data.race_start_info));
    }

    public ValueTask Analyze(SingleModeLegendLoadResponse response)
    {
        if (response.data is not { } data || data.single_mode_load_common is not { } loadCommon)
            return ValueTask.CompletedTask;

        return AnalyzeLegendResponse(new(
            response,
            loadCommon.chara_info,
            loadCommon.home_info,
            data.legend_data_set,
            loadCommon.unchecked_event_array,
            loadCommon.race_start_info));
    }

    ValueTask AnalyzeLegendResponse(LegendScenarioResponseData data)
    {
        if (!CanRenderLegendResponse(data))
            return ValueTask.CompletedTask;

        var historyKey = new LegendDisplayHistory.Key(
            data.CharaInfo.single_mode_chara_id,
            data.CharaInfo.turn);
        var turn = new TurnInfoLegend(data);
        var trainStats = LegendTrainingStatsCalculator.CreateTrainStats(turn);
        lock (renderGate)
        {
            var context = new LegendTrainingDisplayContext(data, turn, trainStats, currentTurn);
            var target = Workspace.Create(WorkspaceTitle);

            LegendTrainingDisplay.SetCurrentDisplay(
                this,
                (modifier, switchToWorkspace, isCurrent) =>
                {
                    lock (renderGate)
                    {
                        var builder = LegendTrainingDisplayBuilder.CreateDefault(context);
                        modifier?.Invoke(context, new(builder));
                        var content = LegendTrainingDisplayRenderer.Render(builder);
                        if (!isCurrent())
                            return false;
                        history.Publish(
                            target,
                            historyKey,
                            content,
                            switchToWorkspace,
                            () => workspace = target);
                        return true;
                    }
                });

            workspace = target;
            if (data.Stage == LegendScenarioStage.Training)
                currentTurn = turn.Turn;
        }

        return ValueTask.CompletedTask;
    }

    static bool CanRenderLegendResponse(LegendScenarioResponseData data)
    {
        if (data.CharaInfo is null || data.HomeInfo?.command_info_array is not { } homeCommands)
            return false;
        if (data.CharaInfo.state is 2 or 3)
            return false;
        if (data.Stage == LegendScenarioStage.Training && data.RaceStartInfo is not null)
            return false;
        if (data.DataSet?.command_info_array is not { } legendCommands ||
            data.DataSet.gauge_count_array is null ||
            data.DataSet.buff_info_array is null)
        {
            return false;
        }

        if (data.Stage == LegendScenarioStage.None)
            return false;

        return TurnInfoLegend.BaseTrainIds.All(trainId =>
            homeCommands.Any(command =>
                TurnInfoLegend.ToTrainId.TryGetValue(command.command_id, out var baseTrainId)
                && baseTrainId == trainId)
            && legendCommands.Any(command =>
                TurnInfoLegend.ToTrainId.TryGetValue(command.command_id, out var baseTrainId)
                && baseTrainId == trainId));
    }
}
