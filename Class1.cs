using Gallop;
using Gallop.Endpoints;
using UmamusumeResponseAnalyzer.LiveDisplay;
using UmamusumeResponseAnalyzer.Plugin;

namespace LegendScenarioAnalyzer;

public sealed class LegendScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "LegendScenarioAnalyzer";
    const string TrainingPanelKey = "training";

    ILiveDisplayOutput? liveDisplay;
    LiveDisplayWorkspace? workspace;
    bool checkedBootstrapWorkspace;
    int currentTurn;

    public string Name => "LegendScenarioAnalyzer";

    public string Author => "UmaAi Team";

    public string[] Targets => [];

    public void Initialize(IPluginContext context)
    {
        liveDisplay = context.LiveDisplay;
        workspace = LiveDisplay.CreateWorkspace(WorkspaceTitle);
        checkedBootstrapWorkspace = false;
        currentTurn = 0;
    }

    public void Dispose()
    {
        LegendTrainingDisplay.ClearCurrentDisplay(this);
        if (liveDisplay is { } output && workspace is { } ownedWorkspace)
            output.RemoveWorkspace(ownedWorkspace);

        workspace = null;
        liveDisplay = null;
    }

    [ResponseAnalyzer<GameApi.SingleModeLegend.ChangeShortCut>(1)]
    public ValueTask Analyze(SingleModeLegendChangeShortCutResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.Load>(1)]
    public ValueTask Analyze(SingleModeLegendLoadResponse response)
    {
        if (response.data is not { } data || data.single_mode_load_common is not { } loadCommon)
            return ValueTask.CompletedTask;

        return AnalyzeLegendResponse(
            response,
            loadCommon.chara_info,
            loadCommon.home_info,
            data.legend_data_set,
            loadCommon.unchecked_event_array,
            loadCommon.race_start_info);
    }

    [ResponseAnalyzer<GameApi.SingleModeLegend.CheckEvent>(1)]
    public ValueTask Analyze(SingleModeLegendCheckEventResponse response)
    {
        if (response.data is not { } data)
            return ValueTask.CompletedTask;

        return AnalyzeLegendResponse(
            response,
            data.chara_info,
            data.home_info,
            data.legend_data_set,
            data.unchecked_event_array,
            data.race_start_info);
    }

    [ResponseAnalyzer<GameApi.SingleModeLegend.CmEnd>(1)]
    public ValueTask Analyze(SingleModeLegendCmEndResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.Continue>(1)]
    public ValueTask Analyze(SingleModeLegendContinueResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set,
                data.unchecked_event_array,
                data.race_start_info)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.ExecCommand>(1)]
    public ValueTask Analyze(SingleModeLegendExecCommandResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.FinishClawCrane>(1)]
    public ValueTask Analyze(SingleModeLegendFinishClawCraneResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.GainSkills>(1)]
    public ValueTask Analyze(SingleModeLegendGainSkillsResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.LegendRaceContinue>(1)]
    public ValueTask Analyze(SingleModeLegendLegendRaceContinueResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set,
                raceStartInfo: data.race_start_info)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.LegendRaceEnd>(1)]
    public ValueTask Analyze(SingleModeLegendLegendRaceEndResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.LegendRaceEntry>(1)]
    public ValueTask Analyze(SingleModeLegendLegendRaceEntryResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set,
                raceStartInfo: data.race_start_info)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.LegendRaceOut>(1)]
    public ValueTask Analyze(SingleModeLegendLegendRaceOutResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.LegendRaceStart>(1)]
    public ValueTask Analyze(SingleModeLegendLegendRaceStartResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                homeInfo: null,
                data.legend_data_set,
                raceStartInfo: data.race_start_info)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.PopularityEnd>(1)]
    public ValueTask Analyze(SingleModeLegendPopularityEndResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.RaceEnd>(1)]
    public ValueTask Analyze(SingleModeLegendRaceEndResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.RaceEntry>(1)]
    public ValueTask Analyze(SingleModeLegendRaceEntryResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set,
                data.unchecked_event_array,
                data.race_start_info)
            : ValueTask.CompletedTask;

    [ResponseAnalyzer<GameApi.SingleModeLegend.RaceOut>(1)]
    public ValueTask Analyze(SingleModeLegendRaceOutResponse response)
        => response.data is { } data
            ? AnalyzeLegendResponse(
                response,
                data.chara_info,
                data.home_info,
                data.legend_data_set,
                data.unchecked_event_array)
            : ValueTask.CompletedTask;

    ValueTask AnalyzeLegendResponse(
        object response,
        SingleModeChara charaInfo,
        SingleModeHomeInfo? homeInfo,
        SingleModeLegendDataSet? dataSet,
        SingleModeEventInfo[]? uncheckedEventArray = null,
        SingleRaceStartInfo? raceStartInfo = null)
        => AnalyzeLegendResponse(new(
            response,
            charaInfo,
            homeInfo,
            dataSet,
            uncheckedEventArray,
            raceStartInfo));

    ValueTask AnalyzeLegendResponse(LegendScenarioResponseData data)
    {
        if (!CanRenderLegendResponse(data))
            return ValueTask.CompletedTask;

        var turn = new TurnInfoLegend(data);
        var trainStats = LegendTrainingStatsCalculator.CreateTrainStats(turn);
        var context = new LegendTrainingDisplayContext(data, turn, trainStats, currentTurn);

        if (data.Stage == LegendScenarioStage.Training)
            currentTurn = turn.Turn;

        LegendTrainingDisplay.SetCurrentDisplay(
            this,
            (extraModifier, switchToWorkspace) => RenderTrainingDisplay(context, extraModifier, switchToWorkspace));
        RenderTrainingDisplay(context, extraModifier: null);
        return ValueTask.CompletedTask;
    }

    void RenderTrainingDisplay(
        LegendTrainingDisplayContext context,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>? extraModifier,
        bool switchToWorkspace = true)
    {
        var builder = LegendTrainingDisplayBuilder.CreateDefault(context);
        ApplyDisplayModifiers(context, builder);
        if (extraModifier is not null)
            ApplyDisplayModifier(context, builder, extraModifier);

        var content = LegendTrainingDisplayRenderer.Render(builder);
        if (switchToWorkspace)
            SwitchFromBootstrapOnFirstActivation();
        LiveDisplay.SetPanel(
            Workspace,
            TrainingPanelKey,
            "传奇杯训练",
            content,
            fullBleed: true,
            switchToWorkspace: switchToWorkspace);
    }

    void ApplyDisplayModifiers(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder)
    {
        foreach (var modifier in LegendTrainingDisplayRegistry.Snapshot())
            ApplyDisplayModifier(context, builder, modifier);
    }

    void ApplyDisplayModifier(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier)
    {
        try
        {
            modifier(context, new(builder));
        }
        catch (Exception ex)
        {
            LiveDisplay.Log(Workspace, $"Legend 训练显示 patch 执行失败: {ex.Message}", LiveDisplaySeverity.Error);
#if DEBUG
            throw;
#endif
        }
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

    void SwitchFromBootstrapOnFirstActivation()
    {
        if (checkedBootstrapWorkspace)
            return;

        checkedBootstrapWorkspace = true;
        if (LiveDisplay.CurrentWorkspace?.Title == "启动")
            LiveDisplay.SwitchWorkspace(Workspace);
    }

    ILiveDisplayOutput LiveDisplay => liveDisplay
        ?? throw new InvalidOperationException("LegendScenarioAnalyzer 尚未初始化 LiveDisplay。");

    LiveDisplayWorkspace Workspace => workspace
        ?? throw new InvalidOperationException("LegendScenarioAnalyzer 尚未创建 LiveDisplay workspace。");
}
