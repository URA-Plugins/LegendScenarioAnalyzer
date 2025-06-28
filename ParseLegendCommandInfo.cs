using EventLoggerPlugin;
using Gallop;
using Spectre.Console;
using UmamusumeResponseAnalyzer.Game.TurnInfo;
using UmamusumeResponseAnalyzer.LocalizedLayout.Handlers;
using static LegendScenarioAnalyzer.i18n.Game;

namespace LegendScenarioAnalyzer
{
    public static class LegendHandler
    {
        //public static class LegendToSave
        //{
        //
        //    public static int lastTurn = 0;
        //    public static string lastDataToSave="";
        //}

        public static int GetCommandInfoStage_legend(SingleModeCheckEventResponse @event)
        {
            //if ((@event.data.unchecked_event_array != null && @event.data.unchecked_event_array.Length > 0)) return;
            if (@event.data.chara_info.playing_state == 1 && (@event.data.unchecked_event_array == null || @event.data.unchecked_event_array.Length == 0))
            {
                return 2;
            } //常规训练
            else if (@event.data.chara_info.playing_state == 5 && @event.data.unchecked_event_array.Any(x => x.story_id == 400010112)) //选buff
            {
                return 5;
            }
            else if (@event.data.chara_info.playing_state == 5 &&
                (@event.data.unchecked_event_array.Any(x => x.story_id == 830241003))) //选团卡事件
            {
                return 3;
            }
            else
            {
                return 0;
            }
        }
        public static void ParseLegendCommandInfo(SingleModeCheckEventResponse @event)
        {
            //var thisturn = @event.data.chara_info.turn;
            //if(thisturn>LegendToSave.lastTurn && LegendToSave.lastDataToSave != "")
            //{
            //    File.AppendAllText($"ura_statistic.txt", LegendToSave.lastDataToSave+"\n");
            //    LegendToSave.lastTurn = thisturn;
            //    LegendToSave.lastDataToSave = "";
            //}
            //if(@event.data.legend_data_set!=null && @event.data.legend_data_set.obtainable_buff_id_array != null && @event.data.legend_data_set.obtainable_buff_id_array.Length>0)
            //{
            //    var obtainableBuffIdArray = @event.data.legend_data_set.obtainable_buff_id_array;
            //    var datatosave = string.Join(" ", obtainableBuffIdArray);
            //    datatosave = $"{@event.data_headers.servertime} {@event.data.chara_info.turn} {datatosave}";
            //    LegendToSave.lastTurn = thisturn;
            //    LegendToSave.lastDataToSave = datatosave;
            //    AnsiConsole.MarkupLine(LegendToSave.lastDataToSave);
            //}

            var stage = GetCommandInfoStage_legend(@event);
            if (stage == 0)
                return;
            // 载入剧本Buff数据csv
            if (GameGlobal.LegendBuffInfo.Count == 0)
                GameGlobal.LoadLegendBuffs();

            var layout = new Layout().SplitColumns(
                new Layout("Main").Size(CommandInfoLayout.Current.MainSectionWidth).SplitRows(
                    new Layout("体力干劲条").SplitColumns(
                        new Layout("日期").Ratio(4),
                        new Layout("总属性").Ratio(6),
                        new Layout("体力").Ratio(6),
                        new Layout("干劲").Ratio(3)).Size(3),
                    new Layout("重要信息").Size(5),
                    new Layout("剧本信息").SplitColumns(
                        new Layout("心得周期").Ratio(3),
                        new Layout("心得等级").Ratio(3),
                        new Layout("心得颜色").Ratio(6)
                        ).Size(3),
                    //new Layout("分割", new Rule()).Size(1),
                    new Layout("训练信息")  // size 20, 共约30行
                    ).Ratio(4),
                new Layout("Ext").Ratio(1)
                );
            var noTrainingTable = false;
            var critInfos = new List<string>();
            var turn = new TurnInfoLegend(@event.data);
            var eventLegendDataset = @event.data.legend_data_set;

            if (GameStats.currentTurn != turn.Turn - 1 //正常情况
                && GameStats.currentTurn != turn.Turn //重复显示
                && turn.Turn != 1 //第一个回合
                )
            {
                GameStats.isFullGame = false;
                critInfos.Add(string.Format(I18N_WrongTurnAlert, GameStats.currentTurn, turn.Turn));
                EventLogger.Init(@event);
            }
            else if (turn.Turn == 1)
            {
                GameStats.isFullGame = true;
                EventLogger.Init(@event);
            }

            //买技能，大师杯剧本年末比赛，会重复显示
            if (@event.data.chara_info.playing_state != 1)
            {
                critInfos.Add(I18N_RepeatTurn);
            }
            else
            {
                //初始化TurnStats
                GameStats.whichScenario = @event.data.chara_info.scenario_id;
                GameStats.currentTurn = turn.Turn;
                GameStats.stats[turn.Turn] = new TurnStats();
                EventLogger.Update(@event);
            }
            var trainItems = new Dictionary<int, SingleModeCommandInfo>
            {
                { 101, @event.data.home_info.command_info_array[0] },
                { 105, @event.data.home_info.command_info_array[1] },
                { 102, @event.data.home_info.command_info_array[2] },
                { 103, @event.data.home_info.command_info_array[3] },
                { 106, @event.data.home_info.command_info_array[4] }
            };
            var trainStats = new TrainStats[5];
            var turnStat = @event.data.chara_info.playing_state != 1 ? new TurnStats() : GameStats.stats[turn.Turn];
            turnStat.motivation = @event.data.chara_info.motivation;
            var failureRate = new Dictionary<int, int>();

            // 总属性计算
            var currentFiveValue = new int[]
            {
                @event.data.chara_info.speed,
                @event.data.chara_info.stamina,
                @event.data.chara_info.power ,
                @event.data.chara_info.guts ,
                @event.data.chara_info.wiz ,
            };
            var fiveValueMaxRevised = new int[]
            {
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_speed),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_stamina),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_power) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_guts) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_wiz) ,
            };
            var currentFiveValueRevised = currentFiveValue.Select(x => ScoreUtils.ReviseOver1200(x)).ToArray();
            var totalValue = currentFiveValueRevised.Sum();
            var totalValueWithPt = totalValue + @event.data.chara_info.skill_point;

            for (var i = 0; i < 5; i++)
            {
                var trainId = GameGlobal.TrainIds[i];
                failureRate[trainId] = trainItems[trainId].failure_rate;
                var trainParams = new Dictionary<int, int>()
                {
                    {1,0},
                    {2,0},
                    {3,0},
                    {4,0},
                    {5,0},
                    {30,0},
                    {10,0},
                };
                foreach (var item in turn.GetCommonResponse().home_info.command_info_array)
                {
                    if (GameGlobal.ToTrainId.TryGetValue(item.command_id, out var value) && value == trainId)
                    {
                        foreach (var trainParam in item.params_inc_dec_info_array)
                            trainParams[trainParam.target_type] += trainParam.value;
                    }
                }

                var stats = new TrainStats
                {
                    FailureRate = trainItems[trainId].failure_rate,
                    VitalGain = trainParams[10]
                };
                if (turn.Vital + stats.VitalGain > turn.MaxVital)
                    stats.VitalGain = turn.MaxVital - turn.Vital;
                if (stats.VitalGain < -turn.Vital)
                    stats.VitalGain = -turn.Vital;
                stats.FiveValueGain = [trainParams[1], trainParams[2], trainParams[3], trainParams[4], trainParams[5]];
                stats.PtGain = trainParams[30];

                // 取上半数值
                // cook_data_set.command_info_array和CommandInfo，SingleCommandInfo都不一样，只能直接取
                // 目前放在1200减半之前，不知道对不对
                var cookValueGainUpper = eventLegendDataset.command_info_array
                    .FirstOrDefault(x => x.command_id == trainId || x.command_id == GameGlobal.XiahesuIds[trainId])?.params_inc_dec_info_array;
                if (cookValueGainUpper != null)
                {
                    foreach (var item in cookValueGainUpper)
                    {
                        if (item.target_type == 30)
                            stats.PtGain += item.value;
                        else if (item.target_type <= 5)
                            stats.FiveValueGain[item.target_type - 1] += item.value;
                        else
                            AnsiConsole.MarkupLine("[red]here[/]");
                    }
                }

                for (var j = 0; j < 5; j++)
                    stats.FiveValueGain[j] = ScoreUtils.ReviseOver1200(turn.Stats[j] + stats.FiveValueGain[j]) - ScoreUtils.ReviseOver1200(turn.Stats[j]);

                if (turn.Turn == 1)
                {
                    turnStat.trainLevel[i] = 1;
                    turnStat.trainLevelCount[i] = 0;
                }
                else
                {
                    var lastTrainLevel = GameStats.stats[turn.Turn - 1] != null ? GameStats.stats[turn.Turn - 1].trainLevel[i] : 1;
                    var lastTrainLevelCount = GameStats.stats[turn.Turn - 1] != null ? GameStats.stats[turn.Turn - 1].trainLevelCount[i] : 0;

                    turnStat.trainLevel[i] = lastTrainLevel;
                    turnStat.trainLevelCount[i] = lastTrainLevelCount;
                    if (GameStats.stats[turn.Turn - 1] != null &&
                        GameStats.stats[turn.Turn - 1].playerChoice == GameGlobal.TrainIds[i] &&
                        !GameStats.stats[turn.Turn - 1].isTrainingFailed &&
                        !((turn.Turn - 1 >= 37 && turn.Turn - 1 <= 40) || (turn.Turn - 1 >= 61 && turn.Turn - 1 <= 64))
                        )//上回合点的这个训练，计数+1
                        turnStat.trainLevelCount[i] += 1;
                    if (turnStat.trainLevelCount[i] >= 4)
                    {
                        turnStat.trainLevelCount[i] -= 4;
                        turnStat.trainLevel[i] += 1;
                    }
                    //检查是否有剧本全体训练等级+1
                    if (turn.Turn == 25 || turn.Turn == 37 || turn.Turn == 49)
                        turnStat.trainLevelCount[i] += 4;
                    if (turnStat.trainLevelCount[i] >= 4)
                    {
                        turnStat.trainLevelCount[i] -= 4;
                        turnStat.trainLevel[i] += 1;
                    }

                    if (turnStat.trainLevel[i] >= 5)
                    {
                        turnStat.trainLevel[i] = 5;
                        turnStat.trainLevelCount[i] = 0;
                    }

                    var trainlv = @event.data.chara_info.training_level_info_array.First(x => x.command_id == GameGlobal.TrainIds[i]).level;
                    if (turnStat.trainLevel[i] != trainlv && stage == 2)
                    {
                        //可能是半途开启小黑板，也可能是有未知bug
                        critInfos.Add($"[red]警告：训练等级预测错误，预测{GameGlobal.TrainIds[i]}为lv{turnStat.trainLevel[i]}(+{turnStat.trainLevelCount[i]})，实际为lv{trainlv}[/]");
                        turnStat.trainLevel[i] = trainlv;
                        turnStat.trainLevelCount[i] = 0;//如果是半途开启小黑板，则会在下一次升级时变成正确的计数
                    }
                }

                trainStats[i] = stats;
            }
            if (stage == 2)
            {
                // 把训练等级信息更新到GameStats
                turnStat.fiveTrainStats = trainStats;
                GameStats.stats[turn.Turn] = turnStat;
            }

            //训练或比赛阶段
            if (stage == 2)
            {
                var grids = new Grid();
                grids.AddColumns(6);

                var failureRateStr = new string[5];
                //失败率>=40%标红、>=20%(有可能大失败)标DarkOrange、>0%标黄
                for (var i = 0; i < 5; i++)
                {
                    var thisFailureRate = failureRate[GameGlobal.TrainIds[i]];
                    failureRateStr[i] = thisFailureRate switch
                    {
                        >= 40 => $"[red]({thisFailureRate}%)[/]",
                        >= 20 => $"[darkorange]({thisFailureRate}%)[/]",
                        > 0 => $"[yellow]({thisFailureRate}%)[/]",
                        _ => string.Empty
                    };
                }
                var commands = turn.CommandInfoArray.Select(command =>
                {
                    var table = new Table()
                    .AddColumn(command.TrainIndex switch
                    {
                        1 => $"{I18N_Speed}{failureRateStr[0]}",
                        2 => $"{I18N_Stamina}{failureRateStr[1]}",
                        3 => $"{I18N_Power}{failureRateStr[2]}",
                        4 => $"{I18N_Nuts}{failureRateStr[3]}",
                        5 => $"{I18N_Wiz}{failureRateStr[4]}"
                    });

                    var currentStat = turn.StatsRevised[command.TrainIndex - 1];
                    var statUpToMax = turn.MaxStatsRevised[command.TrainIndex - 1] - currentStat;
                    table.AddRow(I18N_CurrentRemainStat);
                    table.AddRow($"{currentStat}:{statUpToMax switch
                    {
                        > 400 => $"{statUpToMax}",
                        > 200 => $"[yellow]{statUpToMax}[/]",
                        _ => $"[red]{statUpToMax}[/]"
                    }}");
                    table.AddRow(new Rule());

                    var afterVital = trainStats[command.TrainIndex - 1].VitalGain + turn.Vital;
                    table.AddRow(afterVital switch
                    {
                        < 30 => $"{I18N_Vital}:[red]{afterVital}[/]/{turn.MaxVital}",
                        < 50 => $"{I18N_Vital}:[darkorange]{afterVital}[/]/{turn.MaxVital}",
                        < 70 => $"{I18N_Vital}:[yellow]{afterVital}[/]/{turn.MaxVital}",
                        _ => $"{I18N_Vital}:[green]{afterVital}[/]/{turn.MaxVital}"
                    });

                    var gainGauge = turn.CommandGauges[command.CommandId];
                    //gainGauge.Gauge += turn.GaugeCountDictonary[gainGauge.Legend];
                    var preStar = StarCount(turn.GaugeCountDictonary[gainGauge.Legend]);
                    var starDiff = StarCount(gainGauge.Gauge) - preStar;
                    var gaugeId = gainGauge.Legend - 9045;
                    var gaugeText = $"{mainColorText(gaugeId)}{mainColorName(gaugeId)} {turn.GaugeCountDictonary[gainGauge.Legend]}+{gainGauge.Gauge}[/]";
                    table.AddRow($"Lv{command.TrainLevel} | {gaugeText}");
                    table.AddRow(new Rule());

                    var stats = trainStats[command.TrainIndex - 1];
                    var score = stats.FiveValueGain.Sum();
                    if (score == trainStats.Max(x => x.FiveValueGain.Sum()))
                        table.AddRow($"{I18N_StatSimple}:[aqua]{score}[/]|Pt:{stats.PtGain}");
                    else
                        table.AddRow($"{I18N_StatSimple}:{score}|Pt:{stats.PtGain}");

                    foreach (var trainingPartner in command.TrainingPartners)
                    {
                        table.AddRow(trainingPartner.Name);
                        if (trainingPartner.Shining)
                            table.BorderColor(Color.LightGreen);
                    }
                    for (var i = 5 - command.TrainingPartners.Count(); i > 0; i--)
                    {
                        table.AddRow(string.Empty);
                    }
                    table.AddRow(new Rule());

                    return new Padder(table).Padding(0, 0, 0, 0);
                }); // foreach command
                grids.AddRow([.. commands]);
                layout["训练信息"].Update(grids);

                //女神情热状态，不统计女神召唤次数
                if (@event.data.chara_info.chara_effect_id_array.Any(x => x == 104))
                {
                    turnStat.legend_friendClickEventCountConcerned = false;
                    turnStat.legend_isEffect104 = true;
                    //统计一下女神情热持续了几回合
                    var continuousTurnNum = 0;
                    for (var i = turn.Turn; i >= 1; i--)
                    {
                        if (GameStats.stats[i] == null || !GameStats.stats[i].legend_isEffect104)
                            break;
                        continuousTurnNum++;
                    }
                    AnsiConsole.MarkupLine($"团卡彩圈已持续[green]{continuousTurnNum}[/]回合");
                }
            }
            else
            {
                var grids = new Grid();
                grids.AddColumns(1);
                grids.AddRow([$"非训练阶段，stage={stage}"]);
                layout["训练信息"].Update(grids);
                noTrainingTable = true;
            }

            // 额外信息
            var exTable = new Table().AddColumn("Extras");
            exTable.HideHeaders();
            // 计算连续事件表现
            var eventPerf = EventLogger.PrintCardEventPerf(@event.data.chara_info.scenario_id);
            if (eventPerf.Count > 0)
            {
                exTable.AddRow(new Rule());
                foreach (var row in eventPerf)
                    exTable.AddRow(new Markup(row));
            }
            //exTable.AddRow("asdasdasd");

            layout["日期"].Update(new Panel($"{turn.Year}{I18N_Year} {turn.Month}{I18N_Month}{turn.HalfMonth}").Expand());
            layout["总属性"].Update(new Panel($"[cyan]总属性: {totalValue}[/]").Expand());
            layout["体力"].Update(new Panel($"{I18N_Vital}: [green]{turn.Vital}[/]/{turn.MaxVital}").Expand());
            layout["干劲"].Update(new Panel(@event.data.chara_info.motivation switch
            {
                // 换行分裂和箭头符号有关，去掉
                5 => $"[green]{I18N_MotivationBest}[/]",
                4 => $"[yellow]{I18N_MotivationGood}[/]",
                3 => $"[red]{I18N_MotivationNormal}[/]",
                2 => $"[red]{I18N_MotivationBad}[/]",
                1 => $"[red]{I18N_MotivationWorst}[/]"
            }).Expand());

            var availableTrainingCount = @event.data.home_info.command_info_array.Count(x => x.is_enable == 1);
            if (availableTrainingCount <= 1)
            {
                critInfos.Add("[aqua]非训练回合[/]");
            }
            layout["重要信息"].Update(new Panel(string.Join(Environment.NewLine, critInfos)).Expand());

            var buffPeriod = (turn.Turn - 1) % 6 + 1;
            var buffPeriodColor = buffPeriod switch
            {
                1 => "white",
                2 => "white",
                3 => "white",
                4 => "yellow",   // 等于 4 显示黄色
                _ => "red"       // 其他情况（5 或 6）显示红色
            };
            layout["心得周期"].Update(new Panel($"心得回合周期 [{buffPeriodColor}]{buffPeriod}[/]/6").Expand());

            // 改为一直显示
            var blueBuffCount = @event.data.legend_data_set.buff_info_array.Count(x => x.buff_id / 1000 == 1);
            var greenBuffCount = @event.data.legend_data_set.buff_info_array.Count(x => x.buff_id / 1000 == 2);
            var redBuffCount = @event.data.legend_data_set.buff_info_array.Count(x => x.buff_id / 1000 == 3);
            layout["心得颜色"].Update(new Panel($"当前心得颜色：[cyan]蓝{blueBuffCount}[/] [#00ff00]绿{greenBuffCount}[/] [#ff8080]红{redBuffCount}[/]").Expand());

            if (turn.Turn > 36)
            {
                var mainColor =
                 @event.data.legend_data_set.masterly_bonus_info.info_9046 != null ? 1 :
                 @event.data.legend_data_set.masterly_bonus_info.info_9047 != null ? 2 :
                 @event.data.legend_data_set.masterly_bonus_info.info_9048 != null ? 3 : 0;

                layout["心得颜色"].Update(new Panel($"{mainColorText(mainColor)}主色：{mainColorName(mainColor)}[/]").Expand());
            }

            var gauge_array = @event.data.legend_data_set.gauge_count_array.ToDictionary(x => x.legend_id, x => x.count);
            layout["心得等级"].Update(new Panel($"[cyan]{gauge_array[9046]}/8[/] [#00ff00]{gauge_array[9047]}/8[/] [#ff8080]{gauge_array[9048]}/8[/]").Expand());

            layout["Ext"].Update(exTable);

            GameStats.Print();

            AnsiConsole.Write(layout);
            // 光标倒转一点
            if (noTrainingTable)
                AnsiConsole.Cursor.SetPosition(0, 15);
            else
                AnsiConsole.Cursor.SetPosition(0, 31);

            if (stage == 5)//选buff阶段
            {
                AnsiConsole.MarkupLine("选心得:");
                var obtainableBuffIdArray = @event.data.legend_data_set.obtainable_buff_id_array;
                var buffInfoList = obtainableBuffIdArray
                    .Select(id => GameGlobal.LegendBuffInfo.Find(x => x.buffId == id))
                    .Where(x => x != null)
                    .OrderBy(x => x.color)
                    .ThenBy(x => -x.rank)
                    .ThenBy(x => x.buffId)
                    .ToList();
                foreach (var b in buffInfoList)
                {
                    AnsiConsole.MarkupLine($"{mainColorText(b.color + 1)}☆{b.rank} {b.name} - {b.cn_effect}[/]");
                }
            }

            string GaugeColor((int, int) gain) => gain.Item1 switch
            {
                9046 => $"[#42AEF7]{gain.Item2}[/]",
                9047 => $"[#0BCC58]{gain.Item2}[/]",
                9048 => $"[#F765A4]{gain.Item2}[/]"
            };
            string mainColorText(int which) => which switch
            {
                1 => "[cyan]",
                2 => "[#00ff00]",
                3 => "[#ff8080]",
                _ => "[#ffff00]"
            };
            string mainColorName(int which) => which switch
            {
                1 => "蓝",
                2 => "绿",
                3 => "红",
                _ => "??"
            };
            int StarCount(int gauge) => gauge switch
            {
                8 => 3,
                >= 4 => 2,
                >= 2 => 1,
                _ => 0
            };
        }
    }
}