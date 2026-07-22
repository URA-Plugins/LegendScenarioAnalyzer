using System.Collections.Frozen;
using Gallop;
using UmamusumeResponseAnalyzer;

namespace LegendScenarioAnalyzer;

public enum LegendScenarioStage
{
    None,
    Training,
    GroupCardEvent,
    BuffSelection,
}

public sealed class LegendScenarioResponseData(
    object response,
    SingleModeChara charaInfo,
    SingleModeHomeInfo? homeInfo,
    SingleModeLegendDataSet? dataSet,
    SingleModeEventInfo[]? uncheckedEventArray,
    SingleRaceStartInfo? raceStartInfo)
{
    public object Response { get; } = response;
    public SingleModeChara CharaInfo { get; } = charaInfo;
    public SingleModeHomeInfo? HomeInfo { get; } = homeInfo;
    public SingleModeLegendDataSet? DataSet { get; } = dataSet;
    public SingleModeEventInfo[] UncheckedEventArray { get; } = uncheckedEventArray ?? [];
    public SingleRaceStartInfo? RaceStartInfo { get; } = raceStartInfo;
    public LegendScenarioStage Stage { get; } = GetStage(charaInfo, uncheckedEventArray ?? []);

    public static LegendScenarioStage GetStage(SingleModeChara? charaInfo, SingleModeEventInfo[] uncheckedEvents)
    {
        if (charaInfo is null)
            return LegendScenarioStage.None;

        if (charaInfo.playing_state == 1 && uncheckedEvents.Length == 0)
            return LegendScenarioStage.Training;
        if (charaInfo.playing_state == 5 && uncheckedEvents.Any(x => x.story_id == 400010112))
            return LegendScenarioStage.BuffSelection;
        if (charaInfo.playing_state == 5 && uncheckedEvents.Any(x => x.story_id == 830241003))
            return LegendScenarioStage.GroupCardEvent;
        return LegendScenarioStage.None;
    }
}

public sealed class LegendTrainingDisplayContext(
    LegendScenarioResponseData responseData,
    TurnInfoLegend turn,
    IReadOnlyList<TrainStats> trainStats,
    int previousTurn)
{
    public LegendScenarioResponseData ResponseData { get; } = responseData;
    public TurnInfoLegend Turn { get; } = turn;
    public IReadOnlyList<TrainStats> TrainStats { get; } = trainStats;
    public int PreviousTurn { get; } = previousTurn;
    public SingleModeLegendDataSet DataSet => Turn.DataSet;
}

public sealed class LegendCommandInfo
{
    public LegendCommandInfo(LegendScenarioResponseData response, SingleModeLegendCommandInfo legendCommand)
    {
        CommandId = legendCommand.command_id;
        if (TurnInfoLegend.ToTrainIndex.TryGetValue(CommandId, out var trainIndex))
            TrainIndex = trainIndex + 1;
        var baseCommandId = TurnInfoLegend.ToTrainId[CommandId];

        var training = response.CharaInfo.training_level_info_array
            .FirstOrDefault(x => x.command_id == CommandId || x.command_id == baseCommandId);
        TrainLevel = training is null ? 0 : training.level;

        var homeInfo = response.HomeInfo ?? throw new InvalidOperationException("Legend 训练显示需要 home_info。");
        var normalCommand = homeInfo.command_info_array
            .First(x => TurnInfoLegend.ToTrainId.TryGetValue(x.command_id, out var id) && id == baseCommandId);
        TrainingPartners = normalCommand.training_partner_array
            .Select(x => new TrainingPartner(response, x, normalCommand))
            .OrderBy(x => x.Priority)
            .ToArray();
        GaugeGain = new(legendCommand.legend_id, legendCommand.gain_gauge);
    }

    public int CommandId { get; }
    public int TrainIndex { get; }
    public int TrainLevel { get; }
    public IReadOnlyList<TrainingPartner> TrainingPartners { get; }
    public LegendGaugeGain GaugeGain { get; }
}

public sealed class TrainingPartner
{
    public TrainingPartner(
        LegendScenarioResponseData response,
        int position,
        SingleModeCommandInfo command)
    {
        var supportCard = position is >= 1 and <= 6
            ? Database.Names.GetSupportCard(response.CharaInfo.support_card_array.First(x => x.position == position).support_card_id)
            : null;
        var rawName = supportCard?.Nickname ?? Database.Names.GetCharacter(position).Nickname;
        var friendship = response.CharaInfo.evaluation_info_array.FirstOrDefault(x => x.target_id == position)?.evaluation ?? 0;
        var trainingType = TurnInfoLegend.ToTrainId.TryGetValue(command.command_id, out var baseCommandId)
            ? baseCommandId
            : command.command_id;

        Priority = position is >= 1 and <= 6 ? 0 : 1;
        Shining = supportCard is not null && friendship >= 80 && supportCard.CanTriggerFriendshipTraining(trainingType);
        var displayName = rawName;
        Name = $"{displayName}{(friendship is > 0 and < 100 ? $" {friendship}" : string.Empty)}";
        if (command.tips_event_partner_array.Contains(position))
            Name = $"!{Name}";
    }

    public int Priority { get; }
    public string Name { get; }
    public bool Shining { get; }
}

public sealed record LegendGaugeGain(int LegendId, int GainGauge);

public sealed record TrainStats(int[] FiveValueGain, int PtGain, int VitalGain, int FailureRate);

public sealed class CommandInfoLayout(int trainingCardWidth)
{
    public static CommandInfoLayout Current => Thread.CurrentThread.CurrentUICulture.Name switch
    {
        "zh-CN" => new(17),
        "ja-JP" => new(18),
        _ => new(16)
    };

    public int MainSectionWidth => trainingCardWidth * 5 + 10;
}

public static class ScoreUtils
{
    public static int ReviseOver1200(int value) => value > 1200 ? value * 2 - 1200 : value;
}

public sealed class TurnInfoLegend
{
    public static readonly int[] BaseTrainIds = [101, 105, 102, 103, 106];

    public static readonly FrozenDictionary<int, int> ToTrainId = GameGlobal.ToTrainId;

    public static readonly FrozenDictionary<int, int> ToTrainIndex = GameGlobal.ToTrainIndex;

    public static readonly FrozenDictionary<int, int> XiahesuIds = GameGlobal.XiahesuIds;

    readonly LegendScenarioResponseData response;

    public TurnInfoLegend(LegendScenarioResponseData response)
    {
        this.response = response;
        DataSet = response.DataSet ?? throw new InvalidOperationException("Legend 训练显示需要 legend_data_set。");
        GaugeCounts = DataSet.gauge_count_array.ToDictionary(x => x.legend_id, x => x.count);

        var commandsByBaseTrainId = DataSet.command_info_array
            .Where(x => x.command_type == 1 && ToTrainId.ContainsKey(x.command_id))
            .GroupBy(x => ToTrainId[x.command_id])
            .ToDictionary(x => x.Key, x => x.First());
        CommandInfoArray =
        [
            .. BaseTrainIds.Select(trainId =>
                commandsByBaseTrainId.TryGetValue(trainId, out var command)
                    ? new LegendCommandInfo(response, command)
                    : throw new InvalidOperationException($"Legend 训练显示缺少训练 command: baseCommandId={trainId}"))
        ];
    }

    public SingleModeLegendDataSet DataSet { get; }
    public int Turn => response.CharaInfo.turn;
    public int Year => (Turn - 1) / 24 + 1;
    public int Month => ((Turn - 1) % 24) / 2 + 1;
    public string HalfMonth => Turn % 2 == 0 ? "后半" : "前半";
    public int Vital => response.CharaInfo.vital;
    public int MaxVital => response.CharaInfo.max_vital;
    public int[] Stats => [response.CharaInfo.speed, response.CharaInfo.stamina, response.CharaInfo.power, response.CharaInfo.guts, response.CharaInfo.wiz];
    public int[] StatsRevised => [.. Stats.Select(ScoreUtils.ReviseOver1200)];
    public int[] MaxStatsRevised =>
    [
        ScoreUtils.ReviseOver1200(response.CharaInfo.max_speed),
        ScoreUtils.ReviseOver1200(response.CharaInfo.max_stamina),
        ScoreUtils.ReviseOver1200(response.CharaInfo.max_power),
        ScoreUtils.ReviseOver1200(response.CharaInfo.max_guts),
        ScoreUtils.ReviseOver1200(response.CharaInfo.max_wiz)
    ];
    public Dictionary<int, int> GaugeCounts { get; }
    public IReadOnlyList<LegendCommandInfo> CommandInfoArray { get; }
    public LegendScenarioResponseData GetResponseData() => response;
}

public static class LegendTrainingStatsCalculator
{
    public static IReadOnlyList<TrainStats> CreateTrainStats(TurnInfoLegend turn)
    {
        var response = turn.GetResponseData();
        var homeInfo = response.HomeInfo ?? throw new InvalidOperationException("Legend 训练显示需要 home_info。");
        var homeCommands = homeInfo.command_info_array
            .Where(x => TurnInfoLegend.ToTrainId.ContainsKey(x.command_id))
            .GroupBy(x => TurnInfoLegend.ToTrainId[x.command_id])
            .ToDictionary(x => x.Key, x => x.First());

        var trainStats = new TrainStats[TurnInfoLegend.BaseTrainIds.Length];
        for (var i = 0; i < TurnInfoLegend.BaseTrainIds.Length; i++)
        {
            var trainId = TurnInfoLegend.BaseTrainIds[i];
            var trainParams = new Dictionary<int, int>
            {
                [1] = 0,
                [2] = 0,
                [3] = 0,
                [4] = 0,
                [5] = 0,
                [30] = 0,
                [10] = 0,
            };

            foreach (var item in homeInfo.command_info_array)
            {
                if (!TurnInfoLegend.ToTrainId.TryGetValue(item.command_id, out var value) || value != trainId)
                    continue;

                foreach (var trainParam in item.params_inc_dec_info_array ?? [])
                {
                    if (trainParams.ContainsKey(trainParam.target_type))
                        trainParams[trainParam.target_type] += trainParam.value;
                }
            }

            var vitalGain = trainParams[10];
            if (turn.Vital + vitalGain > turn.MaxVital)
                vitalGain = turn.MaxVital - turn.Vital;
            if (vitalGain < -turn.Vital)
                vitalGain = -turn.Vital;

            var fiveValueGain = new[] { trainParams[1], trainParams[2], trainParams[3], trainParams[4], trainParams[5] };
            var ptGain = trainParams[30];

            var scenarioValueGain = turn.DataSet.command_info_array
                .FirstOrDefault(x => x.command_id == trainId || x.command_id == TurnInfoLegend.XiahesuIds[trainId])
                ?.params_inc_dec_info_array;
            if (scenarioValueGain is not null)
            {
                foreach (var item in scenarioValueGain)
                {
                    if (item.target_type == 30)
                        ptGain += item.value;
                    else if (item.target_type is >= 1 and <= 5)
                        fiveValueGain[item.target_type - 1] += item.value;
                }
            }

            for (var j = 0; j < 5; j++)
                fiveValueGain[j] = ScoreUtils.ReviseOver1200(turn.Stats[j] + fiveValueGain[j]) - ScoreUtils.ReviseOver1200(turn.Stats[j]);

            trainStats[i] = new(
                fiveValueGain,
                ptGain,
                vitalGain,
                homeCommands[trainId].failure_rate);
        }

        return trainStats;
    }
}

internal static class LegendDisplayText
{
    static string Culture => Thread.CurrentThread.CurrentUICulture.Name;

    public static string Year => Culture is "en-US" ? "Year" : "年";

    public static string Month => Culture is "en-US" ? "Month" : "月";

    public static string CurrentRemainStat => Culture switch
    {
        "zh-CN" => "当前:可获得",
        "ja-JP" => "現在：可能",
        _ => "Current: Available"
    };

    public static string StatSimple => Culture switch
    {
        "zh-CN" => "属",
        "ja-JP" => "能",
        _ => "St"
    };

    public static string Vital => Culture is "en-US" ? "Vital" : "体力";

    public static string TrainName(int trainIndex) => trainIndex switch
    {
        1 => Culture switch { "zh-CN" => "速度", "ja-JP" => "スピード", _ => "Speed" },
        2 => Culture switch { "zh-CN" => "耐力", "ja-JP" => "スタミナ", _ => "Stamina" },
        3 => Culture switch { "zh-CN" => "力量", "ja-JP" => "パワー", _ => "Power" },
        4 => Culture switch { "zh-CN" => "根性", "ja-JP" => "根性", _ => "Nuts" },
        5 => Culture switch { "zh-CN" => "智力", "ja-JP" => "賢さ", _ => "Wiz" },
        _ => throw new InvalidOperationException($"未知训练索引: {trainIndex}")
    };

    public static string Motivation(int motivation) => motivation switch
    {
        5 => MotivationBest,
        4 => MotivationGood,
        3 => MotivationNormal,
        2 => MotivationBad,
        1 => MotivationWorst,
        _ => throw new InvalidOperationException($"未知干劲值: {motivation}")
    };

    public static string WrongTurnAlert(int previousTurn, int currentTurn) => Culture switch
    {
        "zh-CN" => $"警告：回合数不正确，上一个回合为{previousTurn}，当前回合为{currentTurn}",
        "ja-JP" => $"警告：ターン数が正しくありません。前のターンは{previousTurn}、現在のターンは{currentTurn}です",
        _ => $"Warning: Incorrect turn, the previous turn was {previousTurn}, the current turn is {currentTurn}"
    };

    public static string StageName(LegendScenarioStage stage) => stage switch
    {
        LegendScenarioStage.Training => "训练阶段",
        LegendScenarioStage.GroupCardEvent => "团卡事件选择阶段",
        LegendScenarioStage.BuffSelection => "心得选择阶段",
        _ => "未知阶段"
    };

    static string MotivationBest => Culture switch { "zh-CN" => "绝好调", "ja-JP" => "絶好調", _ => "Best" };

    static string MotivationGood => Culture switch { "zh-CN" => "好调", "ja-JP" => "好調", _ => "Good" };

    static string MotivationNormal => Culture switch { "zh-CN" => "普通", "ja-JP" => "普通", _ => "Normal" };

    static string MotivationBad => Culture switch { "zh-CN" => "不调", "ja-JP" => "不調", _ => "Bad" };

    static string MotivationWorst => Culture switch { "zh-CN" => "绝不调", "ja-JP" => "絶不調", _ => "Worst" };
}
