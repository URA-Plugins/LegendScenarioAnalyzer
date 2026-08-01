using Gallop;
using Gallop.Endpoints;
using UmamusumeResponseAnalyzer.TerminalGui;
using UmamusumeResponseAnalyzer.Plugin;

namespace LegendScenarioAnalyzer;

public sealed class LegendScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "LegendScenarioAnalyzer";
    const string TrainingPanelKey = "training";

    [ThreadStatic]
    static (Func<bool> TryBegin, Action End)? currentDisplayCommit;

    readonly object renderGate = new();
    Workspace? workspace;
    TaskCompletionSource<object?>? publishingDrained;
    TaskCompletionSource<Exception?>? cleanupInProgress;
    long generation;
    int publishing;
    bool acceptingCallbacks;
    int checkedBootstrapWorkspace;
    int currentTurn;

    public string Name => "LegendScenarioAnalyzer";

    public string Author => "UmaAi Team";

    public string[] Targets => [];

    public static bool WithCurrentDisplayCommit(
        Func<bool> tryBeginCommit,
        Action endCommit,
        Func<bool> modifyCurrent)
    {
        ArgumentNullException.ThrowIfNull(tryBeginCommit);
        ArgumentNullException.ThrowIfNull(endCommit);
        ArgumentNullException.ThrowIfNull(modifyCurrent);

        var previousCommit = currentDisplayCommit;
        currentDisplayCommit = (tryBeginCommit, endCommit);
        try
        {
            return modifyCurrent();
        }
        finally
        {
            currentDisplayCommit = previousCommit;
        }
    }

    public void Initialize(IPluginContext context)
    {
        lock (renderGate)
        {
            acceptingCallbacks = true;
            generation++;
            checkedBootstrapWorkspace = 0;
            currentTurn = 0;
        }
    }

    public void Dispose()
    {
        Task<Exception?>? existingCleanup = null;
        TaskCompletionSource<Exception?>? ownedCleanup = null;
        Task? publishWait = null;
        lock (renderGate)
        {
            if (cleanupInProgress is { } currentCleanup)
            {
                existingCleanup = currentCleanup.Task;
            }
            else
            {
                acceptingCallbacks = false;
                generation++;
                ownedCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
                cleanupInProgress = ownedCleanup;
                publishWait = publishingDrained?.Task;
            }
        }

        if (existingCleanup is not null)
        {
            var priorFailure = existingCleanup.GetAwaiter().GetResult();
            if (priorFailure is not null)
                throw priorFailure;
            return;
        }

        publishWait?.GetAwaiter().GetResult();
        Exception? failure = null;
        try
        {
            LegendTrainingDisplay.ClearCurrentDisplay(this);

            Workspace? target;
            lock (renderGate)
                target = workspace;

            if (target is not null)
            {
                target.RemovePanel(TrainingPanelKey);
                lock (renderGate)
                {
                    if (ReferenceEquals(workspace, target))
                        workspace = null;
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            ownedCleanup!.TrySetResult(failure);
            lock (renderGate)
            {
                if (ReferenceEquals(cleanupInProgress, ownedCleanup))
                    cleanupInProgress = null;
            }
        }

        if (failure is not null)
            throw failure;
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
        long callbackGeneration;
        int previousTurn;
        lock (renderGate)
        {
            if (!acceptingCallbacks)
                return ValueTask.CompletedTask;

            callbackGeneration = generation;
            previousTurn = currentTurn;
            if (data.Stage == LegendScenarioStage.Training)
                currentTurn = turn.Turn;
        }

        var context = new LegendTrainingDisplayContext(data, turn, trainStats, previousTurn);
        if (!TryBeginPublish(callbackGeneration))
            return ValueTask.CompletedTask;
        try
        {
            LegendTrainingDisplay.SetCurrentDisplay(
                this,
                (extraModifier, switchToWorkspace) => RenderTrainingDisplay(
                    context,
                    callbackGeneration,
                    extraModifier,
                    currentDisplayCommit,
                    switchToWorkspace));
        }
        finally
        {
            EndPublish(target: null, published: false);
        }

        RenderTrainingDisplay(
            context,
            callbackGeneration,
            extraModifier: null,
            switchToWorkspace: false,
            switchFromBootstrap: true);
        return ValueTask.CompletedTask;
    }

    void RenderTrainingDisplay(
        LegendTrainingDisplayContext context,
        long callbackGeneration,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>? extraModifier,
        (Func<bool> TryBegin, Action End)? externalCommit = null,
        bool switchToWorkspace = true,
        bool switchFromBootstrap = false)
    {
        lock (renderGate)
        {
            if (!acceptingCallbacks || generation != callbackGeneration)
                return;
        }

        var builder = LegendTrainingDisplayBuilder.CreateDefault(context);
        ApplyDisplayModifiers(context, builder, callbackGeneration);
        if (extraModifier is not null)
            ApplyDisplayModifier(context, builder, extraModifier, callbackGeneration);

        var content = LegendTrainingDisplayRenderer.Render(builder);
        if (!TryBeginPublish(callbackGeneration))
            return;

        Workspace? target = null;
        var published = false;
        Action? endExternalCommit = null;
        try
        {
            if (externalCommit is { } commit)
            {
                if (!commit.TryBegin())
                    return;
                endExternalCommit = commit.End;
            }

            target = Workspace.Create(WorkspaceTitle);
            if (switchFromBootstrap)
                SwitchFromBootstrapOnFirstActivation(target);
            target.SetPanel(
                TrainingPanelKey,
                "传奇杯训练",
                content,
                fullBleed: true,
                switchToWorkspace: switchToWorkspace);
            published = true;
        }
        finally
        {
            try
            {
                EndPublish(target, published);
            }
            finally
            {
                endExternalCommit?.Invoke();
            }
        }
    }

    void ApplyDisplayModifiers(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder,
        long callbackGeneration)
    {
        foreach (var modifier in LegendTrainingDisplayRegistry.Snapshot())
            ApplyDisplayModifier(context, builder, modifier, callbackGeneration);
    }

    void ApplyDisplayModifier(
        LegendTrainingDisplayContext context,
        LegendTrainingDisplayBuilder builder,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier,
        long callbackGeneration)
    {
        try
        {
            modifier(context, new(builder));
        }
        catch (Exception ex)
        {
            PublishPatchError(callbackGeneration, $"Legend 训练显示 patch 执行失败: {ex.Message}");
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

    void SwitchFromBootstrapOnFirstActivation(Workspace target)
    {
        if (Interlocked.Exchange(ref checkedBootstrapWorkspace, 1) != 0)
            return;

        if (Workspace.Current?.Title == "启动")
            target.SwitchTo();
    }

    void PublishPatchError(long callbackGeneration, string message)
    {
        if (!TryBeginPublish(callbackGeneration))
            return;

        try
        {
            Workspace.Create(WorkspaceTitle).Log(message, UiSeverity.Error);
        }
        finally
        {
            EndPublish(target: null, published: false);
        }
    }

    bool TryBeginPublish(long callbackGeneration)
    {
        lock (renderGate)
        {
            if (!acceptingCallbacks || generation != callbackGeneration)
                return false;

            if (publishing++ == 0)
                publishingDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }

    void EndPublish(Workspace? target, bool published)
    {
        TaskCompletionSource<object?>? drained = null;
        lock (renderGate)
        {
            if (published)
                workspace = target;

            if (--publishing == 0)
            {
                drained = publishingDrained;
                publishingDrained = null;
            }
        }

        drained?.TrySetResult(null);
    }
}
