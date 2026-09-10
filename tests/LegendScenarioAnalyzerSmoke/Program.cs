using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Gallop;
using LegendScenarioAnalyzer;
using Newtonsoft.Json;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.Time;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UmamusumeResponseAnalyzer.Entities;
using UmamusumeResponseAnalyzer.TerminalGui;
using UmamusumeResponseAnalyzer.Plugin;
using TColor = Terminal.Gui.Drawing.Color;
using UraConfig = UmamusumeResponseAnalyzer.Config;
using UraDatabase = UmamusumeResponseAnalyzer.Database;
using LegendPlugin = LegendScenarioAnalyzer.LegendScenarioAnalyzer;

var originalCwd = Directory.GetCurrentDirectory();
var originalDisableRealDriverIo = Environment.GetEnvironmentVariable("DisableRealDriverIO");
Environment.SetEnvironmentVariable("DisableRealDriverIO", "1");
var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
var originalCulture = Thread.CurrentThread.CurrentCulture;
Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
var workspace = Path.Combine(Path.GetTempPath(), "ura-legend-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);
Directory.SetCurrentDirectory(workspace);

try
{
    EnsureHostConfigInitialized();
    await InitializeSmokeDatabase(CreateDefaultNames());

    var failures = new List<string>();
    var testCount = 0;
    var rendererTests = new (string Name, Action Run)[]
    {
        ("Terminal.Gui layout, colors, black background, and immutable views", TestTerminalGuiLayoutAndColors),
        ("Terminal.Gui horizontal, vertical, and resize scrolling", TestTerminalGuiScrollingAndResize),
        ("Terminal.Gui buff selection layout and colors", TestTerminalGuiSelectionLayoutAndColors),
        ("Extra sections isolate sources, wrap, resize, and scroll", TestExtraSections)
    };
    foreach (var test in rendererTests)
    {
        testCount++;
        try
        {
            test.Run();
            Console.WriteLine($"PASS {test.Name}");
        }
        catch (Exception ex)
        {
            failures.Add($"{test.Name}: {ex}");
            Console.Error.WriteLine($"FAIL {test.Name}");
            Console.Error.WriteLine(ex);
        }
    }

    using var ui = new WorkspaceSmokeSession();
    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
    var tests = new List<(string Name, Func<ValueTask> Run)>
    {
        ("Initialize and unused Dispose do not create Legend workspace", () => TestWorkspaceLifecycle(ui)),
        ("Initialize registers two Legend response analyzers", () => TestAnalyzerRegistrations(ui)),
        ("CheckEvent renders training workspace panel", () => TestCheckEventRendersTrainingPanel(ui)),
        ("Load renders training workspace panel", () => TestLoadRendersTrainingPanel(ui)),
        ("History uses composite keys, navigates, and trims oldest entries", () => TestHistoryCompositeKeysNavigationAndTrim(ui)),
        ("Hidden DisplayId update is inert until selected", () => TestHiddenDisplayIdUpdateIsInertUntilSelected(ui)),
        ("History limit zero keeps only the live display", () => TestHistoryLimitZeroKeepsLiveDisplay(ui)),
        ("History leaves PageUp and PageDown to the Legend view", () => TestHistoryKeepsPageScrolling(ui)),
        ("Failed DisplayId Show preserves latest workspace", () => TestFailedDisplayIdShowPreservesLatestWorkspace(ui)),
        ("DisplayId producer update and Show publish atomically", () => TestDisplayIdProducerUpdateAndShow(ui)),
        ("Registered modifiers compose, unregister, and publish atomically", () => TestRegisteredModifiers(ui)),
        ("Selection producer update and Show target DisplayId", () => TestSelectionProducerUpdateAndShow(ui)),
        ("30241 group card marks Power friendship training", TestGroupSupportCardMarksFriendshipTraining),
        ("Buff selection requires legend_buff.csv", () => TestBuffSelectionRequiresLegendBuffCsv(ui)),
        ("Buff selection renders non-training panel", () => TestBuffSelectionRendersNonTrainingPanel(ui)),
        ("Buff selection renders nine candidates compactly", () => TestBuffSelectionRendersNineCandidatesCompactly(ui)),
        ("Buff selection tolerates null training params", () => TestBuffSelectionToleratesNullTrainingParams(ui)),
        ("Buff selection load renders despite race start info", () => TestBuffSelectionLoadRendersDespiteRaceStartInfo(ui)),
    };
    var realLoadPacketPath = Environment.GetEnvironmentVariable("LEGEND_LOAD_PACKET");
    if (!string.IsNullOrWhiteSpace(realLoadPacketPath))
        tests.Add(("Real Load packet renders workspace panel", () => TestRealLoadPacketRendersWorkspacePanel(ui, realLoadPacketPath)));

    foreach (var test in tests)
    {
        testCount++;
        try
        {
            await test.Run();
            Console.WriteLine($"PASS {test.Name}");
        }
        catch (Exception ex)
        {
            failures.Add($"{test.Name}: {ex}");
            Console.Error.WriteLine($"FAIL {test.Name}");
            Console.Error.WriteLine(ex);
        }
    }

    if (failures.Count != 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"FAILED Legend smoke tests: {failures.Count}");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(failure);
        }

        Environment.Exit(1);
    }

    Console.WriteLine();
    Console.WriteLine($"PASS Legend smoke tests: {testCount}");
}
finally
{
    Directory.SetCurrentDirectory(originalCwd);
    Thread.CurrentThread.CurrentUICulture = originalUiCulture;
    Thread.CurrentThread.CurrentCulture = originalCulture;
    Environment.SetEnvironmentVariable("DisableRealDriverIO", originalDisableRealDriverIo);
}

static void TestTerminalGuiLayoutAndColors()
{
    var builder = LegendTrainingDisplayBuilder.CreateDefault(
        CreateCheckEventTrainingDisplayContext(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [])));
    builder.ExtraRows.Add("LEGEND-EXTRA");
    var content = LegendTrainingDisplayRenderer.Render(builder);
    builder.ExtraRows.Add("late builder mutation");
    RequireFreshViewFactory(content);
    var capture = CaptureDashboard(content);

    RequireEqual(new Rectangle(0, 0, 120, 36), capture.RootFrame, "Wide root frame");
    RequireEqual(new Size(120, 36), capture.RootContentSize, "Wide root content size");
    RequireEqual(false, capture.HorizontalScrollBarVisible, "Wide horizontal scrollbar");
    if (capture.Text.Contains("late builder mutation", StringComparison.Ordinal))
        throw new InvalidOperationException("Legend render retained the mutable display builder instead of its snapshot.");

    var date = FindCellToken(capture, "1年 1月后半");
    RequireEqual(new Point(2, 1), date.Points[0], "Date origin");
    RequireEqual(new Point(22, 1), FindCellToken(capture, "总属性: 600, Pt: 100").Points[0], "Total origin");
    RequireEqual(new Point(52, 1), FindCellToken(capture, "体力: 80/100").Points[0], "Vital origin");
    RequireEqual(new Point(82, 1), FindCellToken(capture, "绝好调").Points[0], "Motivation origin");
    RequireEqual(new Point(2, 9), FindCellToken(capture, "心得回合周期 2/6").Points[0], "Buff-period origin");
    RequireEqual(new Point(25, 9), FindCellToken(capture, "2/8 4/8 6/8").Points[0], "Gauge-level origin");
    RequireEqual(new Point(49, 9), FindCellToken(capture, "当前心得颜色：蓝1 绿1 红1").Points[0], "Buff-color origin");
    RequireEqual("─", capture.Cells[13, 2].Grapheme, "Training-card rule glyph");

    var titles = new[] { "速度", "耐力(5%)", "力量(10%)", "根性(15%)", "智力(20%)" };
    var trainingColumns = new[] { 0, 19, 38, 57, 76 };
    for (var i = 0; i < titles.Length; i++)
    {
        var title = titles[i];
        var token = FindCellToken(capture, title);
        var titleWidth = title.GetColumns();
        var expectedStart = trainingColumns[i] + (19 - titleWidth) / 2;
        RequireEqual(new Point(expectedStart, 12), token.Points[0], $"{title} title origin");
        RequireEqual(token.Points[0].X + 2, token.Points[1].X, $"{title} CJK cell width");
        RequireBlackBackground(capture, new(expectedStart - 1, 12), $"{title} title left blank");
        RequireBlackBackground(capture, new(expectedStart + titleWidth, 12), $"{title} title right blank");
    }

    foreach (var removed in new[] { "== 训练信息 ==", "▶", " ★" })
    {
        if (capture.Text.Contains(removed, StringComparison.Ordinal))
            throw new InvalidOperationException($"Training output retained migration-only marker '{removed}'.");
    }
    if (!capture.Text.Contains("LEGEND-EXTRA", StringComparison.Ordinal)
        || FindCellToken(capture, "LEGEND-EXTRA").Points[0].X < 95)
    {
        throw new InvalidOperationException("Legend Extras did not render in the right-side column.");
    }

    RequireForeground(capture, "总属性: 600, Pt: 100", 0, new(StandardColor.BrightCyan), "Total");
    RequireForeground(capture, "80/100", 0, new(StandardColor.Green), "Vital current value");
    RequireForeground(capture, "绝好调", 0, new(StandardColor.Green), "Best motivation");
    RequireForeground(capture, "(20%)", 0, new(StandardColor.DarkOrange), "20 percent failure rate");
    RequireForeground(capture, "属:120|Pt:30", 2, new(StandardColor.BrightCyan), "Maximum stat gain");
    RequireForeground(capture, "2/8 4/8 6/8", 0, new(StandardColor.BrightCyan), "Blue gauge");
    RequireForeground(capture, "2/8 4/8 6/8", 4, new(StandardColor.BrightGreen), "Green gauge");
    RequireForeground(capture, "2/8 4/8 6/8", 8, new(StandardColor.BrightRed), "Red gauge");
    RequireForeground(capture, "[速]ライス80", 0, new(StandardColor.BrightCyan), "Friendship-training support name");
    RequireForeground(capture, "[速]ライス80", 6, new(StandardColor.BrightRed), "Friendship below 100");
    RequireBorderForeground(capture, new(0, 11), new(StandardColor.LightGreen), "Shining training border");

    RequireBlackBackground(
        capture,
        new(date.Points[0].X + "1年 1月后半".GetColumns(), date.Points[0].Y),
        "Date header text tail");
    RequireBlackBackground(capture, new(2, 4), "Empty Important row");
    RequireBlackBackground(capture, new(97, 3), "Extras blank area");
    RequireBlackBackground(capture, new(2, 22), "Training partner empty slot");
}

static void TestTerminalGuiScrollingAndResize()
{
    var horizontalBuilder = LegendTrainingDisplayBuilder.CreateDefault(
        CreateCheckEventTrainingDisplayContext(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [])));
    horizontalBuilder.ExtraRows.Add("LEGEND-EXTRA-TAIL");
    var resized = CaptureResizeSequence(LegendTrainingDisplayRenderer.Render(horizontalBuilder));

    RequireEqual(new Rectangle(0, 0, 120, 36), resized.Wide.RootFrame, "Wide root frame");
    RequireEqual(false, resized.Wide.HorizontalScrollBarVisible, "Wide horizontal scrollbar");
    RequireEqual(new Size(120, 36), resized.Wide.RootContentSize, "Wide content size");
    RequireEqual(new Rectangle(0, 0, 80, 30), resized.Narrow.RootFrame, "Narrow root frame");
    RequireEqual(true, resized.Narrow.HorizontalScrollBarVisible, "Narrow horizontal scrollbar");
    RequireEqual(new Size(119, 29), resized.Narrow.RootContentSize, "Narrow content size");
    if (resized.Narrow.Text.Contains("LEGEND-EXTRA-TAIL", StringComparison.Ordinal))
        throw new InvalidOperationException("Extras should start outside the initial narrow viewport.");
    if (resized.NarrowScrolled.HorizontalScrollBarValue <= 0
        || !resized.NarrowScrolled.Text.Contains("LEGEND-EXTRA-TAIL", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Horizontal scrolling did not make Legend Extras reachable.");
    }
    RequireEqual(0, resized.Restored.RootViewport.X, "Restored viewport X");
    RequireEqual(false, resized.Restored.HorizontalScrollBarVisible, "Restored horizontal scrollbar");
    RequireEqual(resized.Wide.Text, resized.Restored.Text, "Restored framebuffer");

    var context = CreateCheckEventTrainingDisplayContext(CreateLegendCheckEventResponse(
        playingState: 1,
        uncheckedEvents: []));
    var verticalBuilder = LegendTrainingDisplayBuilder.CreateDefault(context);
    var display = new LegendTrainingDisplayEditor(verticalBuilder);
    display.Training.Modify(LegendTrain.Speed, card =>
    {
        for (var i = 1; i <= 12; i++)
            card.AddDescription($"extra row {i:00}");
        card.AddText("extra tail");
    });
    var vertical = CaptureVerticalScrollSequence(LegendTrainingDisplayRenderer.Render(verticalBuilder));
    RequireEqual(true, vertical.Initial.VerticalScrollBarVisible, "Extended training vertical scrollbar");
    if (!vertical.Initial.Text.Contains("extra row 01", StringComparison.Ordinal)
        || vertical.Initial.Text.Contains("extra tail", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Extended Legend training rows did not start below the initial viewport as expected.");
    }
    if (vertical.Scrolled.VerticalScrollBarValue <= 0
        || !vertical.Scrolled.Text.Contains("extra tail", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Vertical scrolling did not make the appended Legend row reachable.");
    }
}

static void TestTerminalGuiSelectionLayoutAndColors()
{
    WriteLegendBuffCsv();
    var context = CreateCheckEventTrainingDisplayContext(CreateLegendCheckEventResponse(
        playingState: 5,
        uncheckedEvents: [new() { story_id = 400010112 }],
        obtainableBuffIds: [1001, 2001, 3001]));
    var builder = LegendTrainingDisplayBuilder.CreateDefault(context);
    var blue = Require(builder.FindSelectionCardByBuffId(1001), "Blue selection card");
    var green = Require(builder.FindSelectionCardByBuffId(2001), "Green selection card");
    blue.AddRow("AI评分: 42");
    green.AddRow("AI建议: 推荐");
    green.Highlighted = true;
    builder.ExtraRows.Add("SELECTION-EXTRA");
    var capture = CaptureDashboard(LegendTrainingDisplayRenderer.Render(builder));

    var blueLabel = FindCellToken(capture, "选蓝色第 1 个");
    var greenLabel = FindCellToken(capture, "选绿色第 1 个");
    var redLabel = FindCellToken(capture, "选红色第 1 个");
    RequireEqual(blueLabel.Points[0].X, greenLabel.Points[0].X, "Blue/green option column");
    RequireEqual(blueLabel.Points[0].X, redLabel.Points[0].X, "Blue/red option column");
    RequireEqual(11, blueLabel.Points[0].Y, "Blue selection row");
    RequireEqual(13, greenLabel.Points[0].Y, "Green selection row");
    RequireEqual(15, redLabel.Points[0].Y, "Red selection row");
    RequireForeground(capture, "选蓝色第 1 个", 0, new(StandardColor.BrightCyan), "Blue selection accent");
    RequireForeground(capture, "选绿色第 1 个", 0, new(StandardColor.BrightGreen), "Green selection accent");
    RequireForeground(capture, "选红色第 1 个", 0, new(StandardColor.BrightRed), "Red selection accent");
    RequireForeground(capture, "AI评分:42", 0, new(StandardColor.Gray), "Selection score");
    RequireForeground(capture, "AI建议:推荐", 0, new(StandardColor.Gray), "Selection advice");
    RequireForegroundAt(capture, new(0, 11), new(StandardColor.Blue), "Blue selection rail");
    RequireForegroundAt(capture, new(0, 13), new(StandardColor.LightGreen), "Highlighted selection rail");
    RequireForegroundAt(capture, new(0, 15), new(StandardColor.BrightRed), "Red selection rail");

    var titlePoint = FindCellToken(capture, "测试蓝心得").Points[0];
    var titleAttribute = capture.Cells[titlePoint.Y, titlePoint.X].Attribute
        ?? throw new InvalidOperationException("Selection title has no attribute.");
    if (!titleAttribute.Style.HasFlag(TextStyle.Bold))
        throw new InvalidOperationException("Selection title did not retain bold styling.");
    RequireEqual(TColor.Black, titleAttribute.Background, "Selection title background");
    if (FindCellToken(capture, "SELECTION-EXTRA").Points[0].X < 95)
    {
        throw new InvalidOperationException("Selection-stage Extras did not stay in the right column.");
    }
}

static void TestExtraSections()
{
    foreach (var invalid in new[] { string.Empty, " ", "two\nlines", "two\rlines" })
    {
        try
        {
            using var _ = LegendTrainingDisplay.RegisterPartProducer(invalid);
            throw new InvalidOperationException("Legend accepted an empty or multiline Extra source title.");
        }
        catch (ArgumentException)
        {
        }
    }

    var singleBuilder = new LegendTrainingDisplayBuilder();
    var singleRows = new LegendDisplayRows();
    singleRows.Add(LegendDisplayLine.Colored("solo-body", LegendDisplayColor.Yellow));
    singleBuilder.ExtraSections.Add(new("Solo", singleRows));
    var singleContent = LegendTrainingDisplayRenderer.Render(singleBuilder);
    singleRows.Add("late section mutation");
    var single = CaptureDashboard(singleContent);
    RequireForeground(single, "Solo", 0, new(StandardColor.BrightCyan), "Single section title");
    RequireForeground(single, "solo-body", 0, new(StandardColor.BrightYellow), "Single section body");
    var soloTitle = FindCellToken(single, "Solo").Points[0];
    var soloBody = FindCellToken(single, "solo-body").Points[0];
    RequireEqual(soloTitle.X, soloBody.X, "Single section body indentation");
    RequireEqual(soloTitle.Y + 1, soloBody.Y, "Single section row continuity");
    if (single.Text.Contains("late section mutation", StringComparison.Ordinal))
        throw new InvalidOperationException("Legend snapshot retained mutable producer Extra rows.");

    const string wrapped = "123456789012345678901ABCDEFGHIJKLMNO";
    const string eventWrapped = "ABCDEFGHIJKLMNO123456789012345678901";
    const int wideExtraTextWidth = 21;
    var builder = new LegendTrainingDisplayBuilder();
    builder.ExtraRows.Add(LegendDisplayLine.Colored(wrapped, LegendDisplayColor.Green));
    builder.ExtraSections.Add(new("Empty", new LegendDisplayRows()));
    var eventRows = new LegendDisplayRows();
    eventRows.Add(LegendDisplayLine.Colored(eventWrapped, LegendDisplayColor.Yellow));
    builder.ExtraSections.Add(new("EventLogger", eventRows));
    var aiRows = new LegendDisplayRows();
    aiRows.Add("ai-body");
    builder.ExtraSections.Add(new("AI", aiRows));
    var grouped = CaptureDashboard(LegendTrainingDisplayRenderer.Render(builder), height: 100);
    var legendTitle = FindCellToken(grouped, "Legend").Points[0];
    var legendBody = FindCellToken(grouped, wrapped[..wideExtraTextWidth]).Points[0];
    var eventTitle = FindCellToken(grouped, "EventLogger").Points[0];
    var eventBody = FindCellToken(grouped, eventWrapped[..wideExtraTextWidth]).Points[0];
    var aiTitle = FindCellToken(grouped, "AI").Points[0];
    var wrappedHeight = TextFormatter.WordWrapText(wrapped, wideExtraTextWidth).Count();
    RequireEqual(legendTitle.X, legendBody.X, "Legend body indentation");
    RequireEqual(legendBody.Y + wrappedHeight, eventTitle.Y, "EventLogger title after wrapped Legend body");
    RequireEqual(eventTitle.X, eventBody.X, "EventLogger body indentation");
    RequireEqual(
        eventBody.Y + TextFormatter.WordWrapText(eventWrapped, wideExtraTextWidth).Count(),
        aiTitle.Y,
        "AI title after wrapped EventLogger body");
    RequireForeground(grouped, "Legend", 0, new(StandardColor.BrightCyan), "Legend section title");
    RequireForeground(grouped, "EventLogger", 0, new(StandardColor.BrightCyan), "EventLogger section title");
    RequireForeground(grouped, "AI", 0, new(StandardColor.BrightCyan), "AI section title");
    RequireForegroundAt(grouped, legendBody, new(StandardColor.Green), "Legend section body");
    RequireForegroundAt(grouped, eventBody, new(StandardColor.BrightYellow), "EventLogger section body");
    if (grouped.Text.Contains("Empty", StringComparison.Ordinal))
        throw new InvalidOperationException("Legend rendered an empty Extra section.");

    var resizeBuilder = new LegendTrainingDisplayBuilder();
    var resizeRows = new LegendDisplayRows();
    foreach (var index in Enumerable.Range(1, 17))
        resizeRows.Add($"{index:00}abcdefghijklmnopqrs");
    resizeBuilder.ExtraSections.Add(new("EventLogger", resizeRows));
    var resizeTailRows = new LegendDisplayRows();
    resizeTailRows.Add("legend-resize-tail");
    resizeBuilder.ExtraSections.Add(new("AI", resizeTailRows));
    var resized = CaptureResizeSequence(LegendTrainingDisplayRenderer.Render(resizeBuilder));
    if (resized.Narrow.RootContentSize.Height <= resized.Wide.RootContentSize.Height)
    {
        throw new InvalidOperationException(
            $"Narrow Legend Extra width did not increase wrapped content height: wide content={resized.Wide.RootContentSize}, viewport={resized.Wide.RootViewport}; narrow content={resized.Narrow.RootContentSize}, viewport={resized.Narrow.RootViewport}.");
    }
    RequireEqual(resized.Wide.RootContentSize, resized.Restored.RootContentSize, "Restored Legend Extra content size");
    RequireEqual(0, resized.Restored.RootViewport.X, "Restored Legend Extra viewport X");
    RequireEqual(resized.Wide.Text, resized.Restored.Text, "Restored Legend Extra framebuffer");

    var overflowBuilder = new LegendTrainingDisplayBuilder();
    var overflowRows = new LegendDisplayRows();
    foreach (var index in Enumerable.Range(1, 34))
        overflowRows.Add($"中{index:00}abcdefghijklmnopq");
    overflowBuilder.ExtraSections.Add(new("EventLogger", overflowRows));
    var tailRows = new LegendDisplayRows();
    tailRows.Add("legend-extra-tail");
    overflowBuilder.ExtraSections.Add(new("AI", tailRows));
    var overflowContent = LegendTrainingDisplayRenderer.Render(overflowBuilder);
    var scrolled = CaptureVerticalScrollSequence(overflowContent);
    RequireEqual(true, scrolled.Initial.VerticalScrollBarVisible, "Overflow Legend Extra vertical scrollbar");
    if (scrolled.PageDown.VerticalScrollBarValue <= 0)
        throw new InvalidOperationException("PageDown did not scroll the unified Legend dashboard.");
    if (!scrolled.Scrolled.Text.Contains("AI", StringComparison.Ordinal)
        || !scrolled.Scrolled.Text.Contains("legend-extra-tail", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("End did not reach the final Legend Extra section.");
    }
}

static ValueTask TestWorkspaceLifecycle(WorkspaceSmokeSession ui)
{
    var plugin = new LegendPlugin();

    ui.Bootstrap.SwitchTo();
    var before = ui.CaptureScreen();
    plugin.Initialize(new RecordingPluginContext(ui.Application));
    if (!ReferenceEquals(Workspace.Current, ui.Bootstrap)
        || !string.Equals(before, ui.CaptureScreen(), StringComparison.Ordinal))
        throw new InvalidOperationException("Initialize changed the visible Workspace or framebuffer.");

    plugin.Dispose();
    if (!ReferenceEquals(Workspace.Current, ui.Bootstrap)
        || !string.Equals(before, ui.CaptureScreen(), StringComparison.Ordinal))
        throw new InvalidOperationException("Unused Dispose changed the visible Workspace or framebuffer.");
    return ValueTask.CompletedTask;
}

static ValueTask TestAnalyzerRegistrations(WorkspaceSmokeSession ui)
{
    var context = new RecordingPluginContext(ui.Application);
    new LegendPlugin().Initialize(context);

    RequireEqual(2, context.RecordedAnalyzers.Registrations.Count, "Legend analyzer registration count");
    var checkEvent = context.RecordedAnalyzers.Registrations[0];
    RequireEqual(typeof(SingleModeLegendCheckEventResponse), checkEvent.PayloadType, "CheckEvent payload type");
    RequireEqual(AnalyzerKind.Response, checkEvent.Kind, "CheckEvent analyzer kind");
    RequireEqual(1, checkEvent.Priority, "CheckEvent analyzer priority");
    RequireSequence(
        checkEvent.Patterns,
        [EndpointPattern.Regex(
            "/umamusume/single_mode_legend/(?:change_short_cut|check_event|cm_end|continue|exec_command|finish_claw_crane|gain_skills|legend_race_(?:continue|end|entry|out|start)|popularity_end|race_(?:end|entry|out))")],
        "CheckEvent endpoint patterns");

    var load = context.RecordedAnalyzers.Registrations[1];
    RequireEqual(typeof(SingleModeLegendLoadResponse), load.PayloadType, "Load payload type");
    RequireEqual(AnalyzerKind.Response, load.Kind, "Load analyzer kind");
    RequireEqual(1, load.Priority, "Load analyzer priority");
    RequireSequence(
        load.Patterns,
        [EndpointPattern.Exact("/umamusume/single_mode_legend/load")],
        "Load endpoint patterns");
    return ValueTask.CompletedTask;
}

static async ValueTask TestCheckEventRendersTrainingPanel(WorkspaceSmokeSession ui)
{
    var plugin = new LegendPlugin();
    Workspace? target = null;
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        ui.Bootstrap.SwitchTo();
        await plugin.Analyze(CreateLegendCheckEventResponse(playingState: 1, uncheckedEvents: []));
        target = Workspace.Create("LegendScenarioAnalyzer");
        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("CheckEvent did not focus the Legend Workspace.");

        var rendered = CaptureWorkspace(ui, target);
        foreach (var expected in new[] { "速度", "耐力", "力量", "心得等级", "蓝", "绿", "红", "ライス" })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered training panel does not contain '{expected}'.");
        }
        if (rendered.Contains("支援30001", StringComparison.Ordinal))
            throw new InvalidOperationException("Training partner names still expose raw support-card ids.");
        if (!rendered.Contains("ライス80", StringComparison.Ordinal))
            throw new InvalidOperationException("The first CheckEvent packet was not visible in the Legend framebuffer.");

        ui.Bootstrap.SwitchTo();
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 73));
        var currentAfterUpdate = Workspace.Current;

        var updated = CaptureWorkspace(ui, target);
        if (!updated.Contains("ライス73", StringComparison.Ordinal)
            || updated.Contains("ライス80", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The later CheckEvent packet did not update the Legend framebuffer.");
        }
        if (!ReferenceEquals(currentAfterUpdate, target))
            throw new InvalidOperationException("A later CheckEvent update did not focus the Legend Workspace.");
    }
    finally
    {
        plugin.Dispose();
    }

    var published = target ?? throw new InvalidOperationException("Legend did not create its Workspace.");
    published.SwitchTo();
    var disposed = ui.CaptureScreen(200, 100);
    if (disposed.Contains("心得等级", StringComparison.Ordinal)
        || !ReferenceEquals(Workspace.Create("legendscenarioanalyzer"), published))
    {
        throw new InvalidOperationException(
            "Legend Dispose must remove training without removing the original Workspace generation.");
    }
    if (LegendTrainingDisplay.Show(new(0, 0))
        || !string.Equals(disposed, ui.CaptureScreen(200, 100), StringComparison.Ordinal))
    {
        throw new InvalidOperationException("A late Legend display callback rebuilt training after Dispose.");
    }
    ui.SendKey(Key.CursorLeft);
    if (!string.Equals(disposed, ui.CaptureScreen(200, 100), StringComparison.Ordinal))
        throw new InvalidOperationException("Legend history still handled navigation after Dispose.");
    ui.Bootstrap.SwitchTo();
}

static async ValueTask TestLoadRendersTrainingPanel(WorkspaceSmokeSession ui)
{
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        ui.Bootstrap.SwitchTo();
        await plugin.Analyze(CreateLegendLoadResponse());
        var target = Workspace.Create("LegendScenarioAnalyzer");
        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("Load did not focus the Legend Workspace.");
        var rendered = CaptureWorkspace(ui, target);
        foreach (var expected in new[] { "速度", "耐力", "力量", "心得等级", "蓝", "绿", "红", "ライス" })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered load panel does not contain '{expected}'.");
        }
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestHistoryCompositeKeysNavigationAndTrim(WorkspaceSmokeSession ui)
{
    using var settings = new HistoryLimitSettings(3);
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 61,
            singleModeCharaId: 10,
            turn: 1));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 62,
            singleModeCharaId: 10,
            turn: 2));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 63,
            singleModeCharaId: 11,
            turn: 2));

        var target = Workspace.Create("LegendScenarioAnalyzer");
        RequireHistoryDisplay(ui, target, "ライス63", "ライス61", "ライス62");
        ui.SendKey(Key.CursorUp);
        RequireHistoryDisplay(ui, target, "ライス62", "ライス61", "ライス63");
        ui.SendKey(Key.CursorLeft);
        RequireHistoryDisplay(ui, target, "ライス61", "ライス62", "ライス63");
        ui.SendKey(Key.CursorDown);
        RequireHistoryDisplay(ui, target, "ライス62", "ライス61", "ライス63");
        ui.SendKey(Key.CursorRight);
        RequireHistoryDisplay(ui, target, "ライス63", "ライス61", "ライス62");

        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 64,
            singleModeCharaId: 11,
            turn: 2));
        RequireHistoryDisplay(ui, target, "ライス64", "ライス63");

        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 65,
            singleModeCharaId: 12,
            turn: 3));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 66,
            singleModeCharaId: 13,
            turn: 4));
        ui.SendKey(Key.CursorLeft);
        RequireHistoryDisplay(ui, target, "ライス64", "ライス61", "ライス62", "ライス65", "ライス66");
        ui.SendKey(Key.CursorRight);
        RequireHistoryDisplay(ui, target, "ライス66", "ライス61", "ライス62", "ライス64", "ライス65");
    }
    finally
    {
        plugin.Dispose();
        ui.Bootstrap.SwitchTo();
    }
}

static async ValueTask TestHiddenDisplayIdUpdateIsInertUntilSelected(WorkspaceSmokeSession ui)
{
    using var settings = new HistoryLimitSettings(10);
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 71,
            singleModeCharaId: 20,
            turn: 1));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 72,
            singleModeCharaId: 20,
            turn: 2));

        var target = Workspace.Create("LegendScenarioAnalyzer");
        using var producer = LegendTrainingDisplay.RegisterPartProducer("HistoryTest");
        ui.SendKey(Key.CursorLeft);
        RequireHistoryDisplay(ui, target, "ライス71", "ライス72");

        var beforeUpdate = ui.CaptureScreen(200, 100);
        producer.Update(
            new(20, 2),
            (_, display) => display.Extra.AddText("TARGET-ID-UPDATE-MARKER"));
        var afterUpdate = ui.CaptureScreen(200, 100);
        var normalizedBefore = NormalizeNotificationCountdown(beforeUpdate);
        var normalizedAfter = NormalizeNotificationCountdown(afterUpdate);
        if (!string.Equals(normalizedBefore, normalizedAfter, StringComparison.Ordinal))
            throw new InvalidOperationException("Updating a hidden DisplayId changed the framebuffer.");

        ui.SendKey(Key.CursorRight);
        RequireHistoryDisplay(ui, target, "TARGET-ID-UPDATE-MARKER", "ライス71");
        ui.SendKey(Key.CursorUp);
        RequireHistoryDisplay(ui, target, "ライス71", "TARGET-ID-UPDATE-MARKER");
    }
    finally
    {
        plugin.Dispose();
        ui.Bootstrap.SwitchTo();
    }
}

static async ValueTask TestHistoryLimitZeroKeepsLiveDisplay(WorkspaceSmokeSession ui)
{
    using var settings = new HistoryLimitSettings(0);
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 91,
            singleModeCharaId: 40,
            turn: 1));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            friendship: 92,
            singleModeCharaId: 40,
            turn: 2));

        var target = Workspace.Create("LegendScenarioAnalyzer");
        RequireHistoryDisplay(ui, target, "ライス92", "ライス91");
        ui.SendKey(Key.CursorLeft);
        ui.SendKey(Key.CursorUp);
        RequireHistoryDisplay(ui, target, "ライス92", "ライス91");
    }
    finally
    {
        plugin.Dispose();
        ui.Bootstrap.SwitchTo();
    }
}

static async ValueTask TestHistoryKeepsPageScrolling(WorkspaceSmokeSession ui)
{
    using var settings = new HistoryLimitSettings(2);
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            singleModeCharaId: 50,
            turn: 1));
        using var producer = LegendTrainingDisplay.RegisterPartProducer("LiveTest");
        var id = new LegendTrainingDisplayId(50, 1);
        producer.Update(id, (_, display) =>
        {
            foreach (var index in Enumerable.Range(0, 80))
                display.Training.Modify(LegendTrain.Speed, card => card.AddText($"PAGE-ROW-{index:D2}"));
        });
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: true))
            throw new InvalidOperationException("Show did not create the long Legend display.");

        var target = Workspace.Create("LegendScenarioAnalyzer");
        var initial = ui.CaptureScreen(200, 40);
        if (!initial.Contains("PAGE-ROW-00", StringComparison.Ordinal)
            || initial.Contains("PAGE-ROW-79", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Long Legend history view did not start at its first page.");
        }

        var bottom = initial;
        for (var index = 0; index < 100 && !bottom.Contains("PAGE-ROW-79", StringComparison.Ordinal); index++)
        {
            ui.SendKey(Key.PageDown);
            bottom = ui.CaptureScreen(200, 40);
        }
        if (!bottom.Contains("PAGE-ROW-79", StringComparison.Ordinal)
            || bottom.Contains("PAGE-ROW-00", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PageDown did not reach the end of the Legend history view.");
        }

        var restored = bottom;
        for (var index = 0; index < 100 && !restored.Contains("PAGE-ROW-00", StringComparison.Ordinal); index++)
        {
            ui.SendKey(Key.PageUp);
            restored = ui.CaptureScreen(200, 40);
        }
        if (!restored.Contains("PAGE-ROW-00", StringComparison.Ordinal)
            || restored.Contains("PAGE-ROW-79", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PageUp did not return to the start of the Legend history view.");
        }
    }
    finally
    {
        plugin.Dispose();
        ui.Bootstrap.SwitchTo();
    }
}

static async ValueTask TestGroupSupportCardMarksFriendshipTraining()
{
    await InitializeSmokeDatabase([
        new BaseName(9047, "老登", "老登"),
        new SupportCardName(30241, "团体卡", "老登", 0, 9047)
    ]);

    try
    {
        var response = CreateLegendCheckEventResponse(
            playingState: 1,
            uncheckedEvents: [],
            supportCardId: 30241,
            trainingPartnerCommandId: 102,
            friendship: 90);
        var context = CreateCheckEventTrainingDisplayContext(response);
        var builder = LegendTrainingDisplayBuilder.CreateDefault(context);
        var power = Require(builder.TrainingCards.FirstOrDefault(x => x.TrainIndex == 3), "Power card");

        if (!power.Highlighted)
            throw new InvalidOperationException("30241 group support card should highlight Power as friendship training.");

        var groupPartner = context.Turn.CommandInfoArray
            .First(x => x.TrainIndex == 3)
            .TrainingPartners
            .First(x => x.Name.Contains("老登", StringComparison.Ordinal));
        if (!groupPartner.Shining)
            throw new InvalidOperationException("30241 group support card should be marked as shining.");
    }
    finally
    {
        await InitializeSmokeDatabase(CreateDefaultNames());
    }
}

static async ValueTask TestDisplayIdProducerUpdateAndShow(WorkspaceSmokeSession ui)
{
    var plugin = new LegendPlugin();

    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(playingState: 1, uncheckedEvents: []));
        var target = Workspace.Create("LegendScenarioAnalyzer");
        var id = new LegendTrainingDisplayId(0, 2);
        using var producer = LegendTrainingDisplay.RegisterPartProducer("AI");

        ui.Bootstrap.SwitchTo();
        var renderCount = 0;
        producer.Update(id, (_, display) =>
        {
            renderCount++;
            display.Training.Modify(LegendTrain.Power, card =>
            {
                card.AddText("current AI note");
                card.Highlight();
            });
        });
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: true) || renderCount != 1)
            throw new InvalidOperationException("Show should compose and publish the DisplayId exactly once.");

        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("Explicit Show should switch workspace.");
        var modified = CaptureWorkspace(ui, target);
        if (!modified.Contains("current AI note", StringComparison.Ordinal))
            throw new InvalidOperationException("Show did not publish the producer part.");

        ui.Bootstrap.SwitchTo();
        producer.Update(id, (_, display) => display.Extra.AddText("replacement AI note"));
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: true))
            throw new InvalidOperationException("A later Show should publish the replaced producer part.");

        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("A later Show should switch workspace.");
        var replaced = CaptureWorkspace(ui, target);
        if (replaced.Contains("current AI note", StringComparison.Ordinal))
            throw new InvalidOperationException("Same producer and DisplayId must use the last update.");
        if (!replaced.Contains("replacement AI note", StringComparison.Ordinal))
            throw new InvalidOperationException("The replacement producer part was not shown.");

        ui.Bootstrap.SwitchTo();
        producer.Update(id, (_, display) => display.Extra.AddText("no switch current AI note"));
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: false))
            throw new InvalidOperationException("Show should support publishing without switching workspace.");

        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("No-switch Show changed workspace focus.");
        var noSwitchModified = CaptureWorkspace(ui, target);
        if (!noSwitchModified.Contains("no switch current AI note", StringComparison.Ordinal))
            throw new InvalidOperationException("No-switch Show did not publish the producer part.");

        producer.Update(id, (_, display) => display.Extra.AddText("replacement no-switch AI note"));
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: false))
            throw new InvalidOperationException("A later no-switch Show failed.");

        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("A later no-switch Show changed workspace focus.");
        var noSwitchReplaced = CaptureWorkspace(ui, target);
        if (noSwitchReplaced.Contains("no switch current AI note", StringComparison.Ordinal)
            || !noSwitchReplaced.Contains("replacement no-switch AI note", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A later producer update retained stale data.");
        }
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestFailedDisplayIdShowPreservesLatestWorkspace(WorkspaceSmokeSession ui)
{
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(playingState: 1, uncheckedEvents: []));
        var target = Workspace.Create("LegendScenarioAnalyzer");
        _ = CaptureWorkspace(ui, target);
        using var producer = LegendTrainingDisplay.RegisterPartProducer("Failing");
        var id = new LegendTrainingDisplayId(0, 2);
        ui.Bootstrap.SwitchTo();

        try
        {
            producer.Update(id, (_, display) =>
            {
                display.Extra.AddText("legend-partial-sentinel");
                throw new InvalidOperationException("legend-modifier-sentinel");
            });
            LegendTrainingDisplay.Show(id);
            throw new InvalidOperationException("Show did not propagate the producer failure.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "legend-modifier-sentinel")
        {
        }

        var currentAfterFailure = Workspace.Current;
        var after = CaptureWorkspace(ui, target);
        if (!ReferenceEquals(currentAfterFailure, ui.Bootstrap) ||
            after.Contains("legend-partial-sentinel", StringComparison.Ordinal) ||
            !after.Contains("总属性", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A failed Show changed focus or published its partial display.");
        }
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestRegisteredModifiers(WorkspaceSmokeSession ui)
{
    var plugin = new LegendPlugin();
    LegendTrainingDisplayPartProducer? first = null;
    LegendTrainingDisplayPartProducer? second = null;
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(playingState: 1, uncheckedEvents: []));
        var target = Workspace.Create("LegendScenarioAnalyzer");
        var id = new LegendTrainingDisplayId(0, 2);
        ui.Bootstrap.SwitchTo();

        first = LegendTrainingDisplay.RegisterPartProducer("First");
        first.Update(
            id,
            (_, display) =>
                display.Extra.AddStyled(new LegendDisplaySegment("persistent-first", LegendDisplayColor.Cyan)));
        using var empty = LegendTrainingDisplay.RegisterPartProducer("Empty");
        empty.Update(id, (_, _) => { });
        second = LegendTrainingDisplay.RegisterPartProducer("Second");
        second.Update(id, (_, display) => display.Extra.AddText("persistent-second"));
        using var third = LegendTrainingDisplay.RegisterPartProducer("Third");
        third.Update(id, (_, display) => display.Extra.AddText("one-shot"));
        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("Part Update must not switch workspace focus.");

        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: false))
            throw new InvalidOperationException("Show failed with registered parts.");
        var composed = CaptureWorkspace(ui, target);
        var firstTitleIndex = composed.IndexOf("First", StringComparison.Ordinal);
        var firstIndex = composed.IndexOf("persistent-first", StringComparison.Ordinal);
        var secondTitleIndex = composed.IndexOf("Second", StringComparison.Ordinal);
        var secondIndex = composed.IndexOf("persistent-second", StringComparison.Ordinal);
        var thirdTitleIndex = composed.IndexOf("Third", StringComparison.Ordinal);
        var oneShotIndex = composed.IndexOf("one-shot", StringComparison.Ordinal);
        if (firstTitleIndex < 0 || firstIndex <= firstTitleIndex
            || secondTitleIndex <= firstIndex || secondIndex <= secondTitleIndex
            || thirdTitleIndex <= secondIndex || oneShotIndex <= thirdTitleIndex
            || composed.Contains("Empty", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legend Extra sections were not grouped in producer registration order.");
        }

        third.Dispose();
        if (!LegendTrainingDisplay.Show(id))
            throw new InvalidOperationException("Show should rebuild after a producer is disposed.");
        var refreshed = CaptureWorkspace(ui, target);
        if (!refreshed.Contains("persistent-first", StringComparison.Ordinal) ||
            !refreshed.Contains("persistent-second", StringComparison.Ordinal) ||
            refreshed.Contains("one-shot", StringComparison.Ordinal))
            throw new InvalidOperationException("Show did not preserve only registered producer parts.");

        var beforeFailure = refreshed;
        using var failing = LegendTrainingDisplay.RegisterPartProducer("Failing");
        failing.Update(id, (_, _) => throw new InvalidOperationException("persistent-sentinel"));
        try
        {
            LegendTrainingDisplay.Show(id);
            throw new InvalidOperationException("Failing producer part did not propagate its failure.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "persistent-sentinel")
        {
        }
        if (!string.Equals(beforeFailure, CaptureWorkspace(ui, target), StringComparison.Ordinal))
            throw new InvalidOperationException("A failed producer part published a partial display.");
        failing.Dispose();

        first.Dispose();
        first = null;
        if (!LegendTrainingDisplay.Show(id))
            throw new InvalidOperationException("Show failed after disposing one producer.");
        var afterUnregister = CaptureWorkspace(ui, target);
        if (afterUnregister.Contains("persistent-first", StringComparison.Ordinal) ||
            !afterUnregister.Contains("persistent-second", StringComparison.Ordinal))
            throw new InvalidOperationException("Disposing one producer removed the wrong composed content.");
    }
    finally
    {
        second?.Dispose();
        first?.Dispose();
        plugin.Dispose();
    }
}

static async ValueTask TestSelectionProducerUpdateAndShow(WorkspaceSmokeSession ui)
{
    WriteLegendBuffCsv();
    var plugin = new LegendPlugin();

    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 5,
            uncheckedEvents: [new() { story_id = 400010112 }],
            obtainableBuffIds: [1001, 2001]));
        var target = Workspace.Create("LegendScenarioAnalyzer");
        var id = new LegendTrainingDisplayId(0, 2);
        using var producer = LegendTrainingDisplay.RegisterPartProducer("Selection");

        ui.Bootstrap.SwitchTo();
        producer.Update(id, (_, display) =>
        {
            display.Selection.ModifyByBuffId(1001, card =>
            {
                card.AddText("current selection note");
                card.Highlight();
            });
        });
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: true))
            throw new InvalidOperationException("Show should publish the selection part.");

        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("Selection Show should switch workspace.");
        var modified = CaptureWorkspace(ui, target);
        if (!modified.Contains("current selection note", StringComparison.Ordinal))
            throw new InvalidOperationException("Show did not publish the selection part.");

        ui.Bootstrap.SwitchTo();
        producer.Update(
            id,
            (_, display) => display.Selection.ModifyByBuffId(
                1001,
                card => card.AddText("replacement selection note")));
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: true))
            throw new InvalidOperationException("A later selection Show failed.");

        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("A later selection Show should switch workspace.");
        var replaced = CaptureWorkspace(ui, target);
        if (replaced.Contains("current selection note", StringComparison.Ordinal))
            throw new InvalidOperationException("Selection producer retained its previous part.");
        if (!replaced.Contains("replacement selection note", StringComparison.Ordinal))
            throw new InvalidOperationException("Selection producer replacement was not shown.");

        ui.Bootstrap.SwitchTo();
        producer.Update(
            id,
            (_, display) =>
                display.Selection.ModifyByBuffId(
                    1001,
                    card => card.AddText("no switch current selection note")));
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: false))
            throw new InvalidOperationException("Selection Show should support no-switch publishing.");

        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("Selection no-switch Show changed workspace focus.");
        var noSwitchModified = CaptureWorkspace(ui, target);
        if (!noSwitchModified.Contains("no switch current selection note", StringComparison.Ordinal))
            throw new InvalidOperationException("Selection no-switch Show did not apply the part.");

        producer.Update(
            id,
            (_, display) => display.Selection.ModifyByBuffId(
                1001,
                card => card.AddText("replacement no-switch selection note")));
        if (!LegendTrainingDisplay.Show(id, switchToWorkspace: false))
            throw new InvalidOperationException("A later no-switch selection Show failed.");

        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("A later no-switch selection Show changed focus.");
        var noSwitchReplaced = CaptureWorkspace(ui, target);
        if (noSwitchReplaced.Contains("no switch current selection note", StringComparison.Ordinal)
            || !noSwitchReplaced.Contains("replacement no-switch selection note", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A later selection producer update retained stale data.");
        }
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestRealLoadPacketRendersWorkspacePanel(WorkspaceSmokeSession ui, string packetPath)
{
    var response = await ReadLegendLoadPacket(packetPath);
    var loadCommon = response.data.single_mode_load_common;
    var stage = LegendScenarioResponseData.GetStage(loadCommon.chara_info, loadCommon.unchecked_event_array);
    if (stage == LegendScenarioStage.BuffSelection)
        WriteLegendBuffCsv();

    await InitializeSmokeDatabase(CreateNamesForRealLoadPacket(response));

    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        ui.Bootstrap.SwitchTo();
        await plugin.Analyze(response);

        var target = Workspace.Create("LegendScenarioAnalyzer");
        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("Real load should switch to the Legend workspace.");
        var rendered = CaptureWorkspace(ui, target);
        if (!rendered.Contains("传奇杯训练", StringComparison.Ordinal))
            throw new InvalidOperationException("The real Host framebuffer does not show the real-load panel title.");
        if (stage == LegendScenarioStage.BuffSelection)
        {
            foreach (var expected in new[] { "心得选择阶段", "选蓝色第 1 个", "选绿色第 1 个", "选红色第 1 个" })
            {
                if (!rendered.Contains(expected, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Rendered real load selection panel does not contain '{expected}'.");
            }

            return;
        }

        if (stage != LegendScenarioStage.Training)
            throw new InvalidOperationException($"Real load packet stage {stage} should render a supported Legend panel.");

        foreach (var expected in new[] { "速度", "耐力", "力量", "心得等级", "蓝", "绿", "红" })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered real load panel does not contain '{expected}'.");
        }
        var context = CreateLoadTrainingDisplayContext(response);
        var builder = LegendTrainingDisplayBuilder.CreateDefault(context);
        var power = Require(builder.TrainingCards.FirstOrDefault(x => x.TrainIndex == 3), "Real load Power card");
        var groupPartner = context.Turn.CommandInfoArray
            .First(x => x.TrainIndex == 3)
            .TrainingPartners
            .FirstOrDefault(x => x.Name.Contains("老登", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Real load packet did not place 30241 group support card on Power.");

        if (!power.Highlighted)
            throw new InvalidOperationException("Real load 30241 group support card should highlight Power as friendship training.");
        if (!groupPartner.Shining)
            throw new InvalidOperationException("Real load 30241 group support card should be marked as shining.");
    }
    finally
    {
        plugin.Dispose();
        await InitializeSmokeDatabase(CreateDefaultNames());
    }
}

static async ValueTask TestBuffSelectionRendersNonTrainingPanel(WorkspaceSmokeSession ui)
{
    WriteLegendBuffCsv();
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 5,
            uncheckedEvents: [new() { story_id = 400010112 }],
            obtainableBuffIds: [1001, 2001]));

        var target = Workspace.Create("LegendScenarioAnalyzer");
        var rendered = CaptureWorkspace(ui, target);
        foreach (var expected in new[]
                 {
                     "非训练阶段",
                     "选蓝色第 1 个",
                     "选绿色第 1 个",
                     "● ★1",
                     "测试蓝心得",
                     "测试绿心得",
                     "测试蓝效果",
                     "测试绿效果"
                 })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered non-training panel does not contain '{expected}'.");
        }
        var blueLabel = rendered.IndexOf("选蓝色第 1 个", StringComparison.Ordinal);
        var blueName = rendered.IndexOf("测试蓝心得", StringComparison.Ordinal);
        var blueEffect = rendered.IndexOf("测试蓝效果", StringComparison.Ordinal);
        if (blueLabel < 0 || blueName < 0 || blueEffect < 0 || blueLabel > blueName || blueName > blueEffect)
            throw new InvalidOperationException("Buff selection card should present the option label, buff name, and effect in game-like reading order.");
        if (!ContainsRenderedLine(rendered, "选蓝色第 1 个", "测试蓝心得", "测试蓝效果"))
            throw new InvalidOperationException("Buff selection card should keep each candidate on one rendered line.");
        if (rendered.Contains("选心得:", StringComparison.Ordinal))
            throw new InvalidOperationException("Buff selection candidates should render as main-area cards instead of the old Extra list.");
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestBuffSelectionRendersNineCandidatesCompactly(WorkspaceSmokeSession ui)
{
    WriteLegendBuffCsv();
    var plugin = new LegendPlugin();
    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 5,
            uncheckedEvents: [new() { story_id = 400010112 }],
            obtainableBuffIds: [1101, 1102, 1104, 2101, 2102, 2104, 3101, 3103, 3104]));

        var rendered = CaptureWorkspace(ui, Workspace.Create("LegendScenarioAnalyzer"));
        foreach (var expected in new[]
                 {
                     "选蓝色第 1 个",
                     "选蓝色第 3 个",
                     "选绿色第 1 个",
                     "选绿色第 3 个",
                     "选红色第 1 个",
                     "选红色第 3 个"
                 })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered nine-candidate buff selection panel does not contain '{expected}'.");
        }

        var firstSelectionLine = LineIndexContaining(rendered, "选蓝色第 1 个");
        var lastSelectionLine = LineIndexContaining(rendered, "选红色第 3 个");
        if (firstSelectionLine < 0 || lastSelectionLine < 0 || lastSelectionLine - firstSelectionLine > 10)
            throw new InvalidOperationException("Nine buff selection candidates should fit with only color-group spacers.");

        var optionColumns = new[]
        {
            FirstColumnOfLineContaining(rendered, "选蓝色第 1 个"),
            FirstColumnOfLineContaining(rendered, "选绿色第 1 个"),
            FirstColumnOfLineContaining(rendered, "选红色第 1 个")
        };
        if (optionColumns.Any(x => x < 0) || optionColumns.Distinct().Count() != 1)
            throw new InvalidOperationException("Buff selection option labels should align across color groups.");
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestBuffSelectionToleratesNullTrainingParams(WorkspaceSmokeSession ui)
{
    WriteLegendBuffCsv();
    var plugin = new LegendPlugin();
    var response = CreateLegendCheckEventResponse(
        playingState: 5,
        uncheckedEvents: [new() { story_id = 400010112 }],
        obtainableBuffIds: [1001, 2001]);

    foreach (var command in response.data.home_info.command_info_array)
    {
        command.is_enable = 0;
        command.params_inc_dec_info_array = null;
    }

    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        ui.Bootstrap.SwitchTo();
        await plugin.Analyze(response);

        var rendered = CaptureWorkspace(ui, Workspace.Create("LegendScenarioAnalyzer"));
        foreach (var expected in new[] { "心得选择阶段", "选蓝色第 1 个", "选绿色第 1 个" })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered buff selection panel does not contain '{expected}'.");
        }
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestBuffSelectionLoadRendersDespiteRaceStartInfo(WorkspaceSmokeSession ui)
{
    WriteLegendBuffCsv();
    var plugin = new LegendPlugin();
    var response = CreateLegendLoadResponse(
        playingState: 5,
        uncheckedEvents: [new() { story_id = 400010112 }],
        raceStartInfo: new() { program_id = 1094 },
        obtainableBuffIds: [1001, 2001]);

    try
    {
        plugin.Initialize(new RecordingPluginContext(ui.Application));
        ui.Bootstrap.SwitchTo();
        await plugin.Analyze(response);

        var target = Workspace.Create("LegendScenarioAnalyzer");
        if (!ReferenceEquals(Workspace.Current, target))
            throw new InvalidOperationException("Load buff selection should switch to the Legend workspace.");
        var rendered = CaptureWorkspace(ui, target);
        foreach (var expected in new[] { "心得选择阶段", "选蓝色第 1 个", "选绿色第 1 个" })
        {
            if (!rendered.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Rendered load buff selection panel does not contain '{expected}'.");
        }
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask TestBuffSelectionRequiresLegendBuffCsv(WorkspaceSmokeSession ui)
{
    var csvPath = Path.Combine("PluginData", "LegendScenarioAnalyzer", "legend_buff.csv");
    if (File.Exists(csvPath))
        File.Delete(csvPath);

    var plugin = new LegendPlugin();

    plugin.Initialize(new RecordingPluginContext(ui.Application));
    try
    {
        await plugin.Analyze(CreateLegendCheckEventResponse(
            playingState: 5,
            uncheckedEvents: [new() { story_id = 400010112 }]));

        throw new InvalidOperationException("Buff selection should fail clearly when legend_buff.csv is missing.");
    }
    catch (FileNotFoundException ex) when (ex.Message.Contains("legend_buff.csv", StringComparison.Ordinal))
    {
    }
    finally
    {
        plugin.Dispose();
    }
}

static async ValueTask<SingleModeLegendLoadResponse> ReadLegendLoadPacket(string packetPath)
{
    if (!File.Exists(packetPath))
        throw new FileNotFoundException("LEGEND_LOAD_PACKET 指向的文件不存在。", packetPath);

    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(packetPath));
    if (!document.RootElement.TryGetProperty("payload", out var payload))
        throw new InvalidOperationException("LEGEND_LOAD_PACKET 必须是包含 payload 的 URA packet JSON。");

    var data = payload.GetProperty("data");
    var loadCommon = data.GetProperty("single_mode_load_common");
    return new()
    {
        data = new()
        {
            single_mode_load_common = new()
            {
                chara_info = DeserializePacketValue<SingleModeChara>(loadCommon.GetProperty("chara_info")),
                home_info = DeserializePacketValue<SingleModeHomeInfo>(loadCommon.GetProperty("home_info")),
                unchecked_event_array = DeserializePacketValue<SingleModeEventInfo[]>(loadCommon.GetProperty("unchecked_event_array")),
                race_start_info = loadCommon.GetProperty("race_start_info").ValueKind == JsonValueKind.Null
                    ? null
                    : DeserializePacketValue<SingleRaceStartInfo>(loadCommon.GetProperty("race_start_info")),
            },
            legend_data_set = CreateLegendDataSetFromPacket(data.GetProperty("legend_data_set")),
        }
    };
}

static SingleModeLegendDataSet CreateLegendDataSetFromPacket(JsonElement dataSet)
    => new()
    {
        command_info_array = DeserializePacketValue<SingleModeLegendCommandInfo[]>(dataSet.GetProperty("command_info_array")),
        evaluation_info_array = DeserializePacketValue<SingleModeLegendEvaluationInfo[]>(dataSet.GetProperty("evaluation_info_array")),
        gauge_count_array = DeserializePacketValue<SingleModeLegendGauge[]>(dataSet.GetProperty("gauge_count_array")),
        buff_info_array = DeserializePacketValue<SingleModeLegendBuffInfo[]>(dataSet.GetProperty("buff_info_array")),
        obtainable_buff_id_array = dataSet.TryGetProperty("obtainable_buff_id_array", out var obtainableBuffIds)
            ? DeserializePacketValue<int[]>(obtainableBuffIds)
            : [],
        activated_buff_id_array = dataSet.TryGetProperty("activated_buff_id_array", out var activatedBuffIds)
            ? DeserializePacketValue<int[]>(activatedBuffIds)
            : [],
    };

static T DeserializePacketValue<T>(JsonElement element)
    => JsonConvert.DeserializeObject<T>(
            element.GetRawText(),
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
        ?? throw new InvalidOperationException($"LEGEND_LOAD_PACKET 不能反序列化字段 {typeof(T).Name}。");

static List<BaseName> CreateDefaultNames() =>
[
    new BaseName(1001, "ライスシャワー", "ライス"),
    new SupportCardName(30001, "幸せは曲がり角の向こう", "ライス", 101, 1001)
];

static List<BaseName> CreateNamesForRealLoadPacket(SingleModeLegendLoadResponse response)
{
    var names = new Dictionary<int, BaseName>();

    void AddCharacter(int id)
    {
        if (id > 0 && !names.ContainsKey(id))
            names.Add(id, new(id, $"キャラ{id}", $"キャラ{id}"));
    }

    void AddSupportCard(int id, int position)
    {
        if (id <= 0)
            return;

        if (id == 30241)
        {
            AddCharacter(9047);
            names[id] = new SupportCardName(id, "团体卡", "老登", 0, 9047);
            return;
        }

        names[id] = new SupportCardName(id, $"サポート{id}", $"サポート{id}", 101, position);
    }

    var loadCommon = response.data.single_mode_load_common;
    AddCharacter(loadCommon.chara_info.card_id);

    foreach (var supportCard in loadCommon.chara_info.support_card_array ?? [])
    {
        AddCharacter(supportCard.position);
        AddSupportCard(supportCard.support_card_id, supportCard.position);
    }

    foreach (var evaluation in loadCommon.chara_info.evaluation_info_array ?? [])
        AddCharacter(evaluation.target_id);

    foreach (var command in loadCommon.home_info.command_info_array ?? [])
    {
        foreach (var position in command.training_partner_array ?? [])
            AddCharacter(position);
    }

    foreach (var evaluation in response.data.legend_data_set.evaluation_info_array ?? [])
    {
        AddCharacter(evaluation.target_id);
        AddCharacter(evaluation.chara_id);
    }

    return names.Values.ToList();
}

static SingleModeLegendCheckEventResponse CreateLegendCheckEventResponse(
    int playingState,
    SingleModeEventInfo[] uncheckedEvents,
    int supportCardId = 30001,
    int trainingPartnerCommandId = 101,
    int friendship = 80,
    int[]? obtainableBuffIds = null,
    int singleModeCharaId = 0,
    int turn = 2)
    => new()
    {
        data = new()
        {
            chara_info = CreateChara(playingState, supportCardId, friendship, singleModeCharaId, turn),
            home_info = new() { command_info_array = BaseTrainingCommands(trainingPartnerCommandId) },
            unchecked_event_array = uncheckedEvents,
            legend_data_set = CreateLegendDataSet(obtainableBuffIds),
            select_index_info_array = []
        }
    };

static SingleModeLegendLoadResponse CreateLegendLoadResponse(
    int playingState = 1,
    SingleModeEventInfo[]? uncheckedEvents = null,
    SingleRaceStartInfo? raceStartInfo = null,
    int[]? obtainableBuffIds = null)
    => new()
    {
        data = new()
        {
            single_mode_load_common = new()
            {
                chara_info = CreateChara(playingState),
                home_info = new() { command_info_array = BaseTrainingCommands() },
                unchecked_event_array = uncheckedEvents ?? [],
                race_start_info = raceStartInfo
            },
            legend_data_set = CreateLegendDataSet(obtainableBuffIds)
        }
    };

static LegendTrainingDisplayContext CreateCheckEventTrainingDisplayContext(SingleModeLegendCheckEventResponse response)
{
    var data = new LegendScenarioResponseData(
        response,
        response.data.chara_info,
        response.data.home_info,
        response.data.legend_data_set,
        response.data.unchecked_event_array,
        raceStartInfo: null);
    var turn = new TurnInfoLegend(data);
    var trainStats = LegendTrainingStatsCalculator.CreateTrainStats(turn);
    return new(data, turn, trainStats, previousTurn: 1);
}

static LegendTrainingDisplayContext CreateLoadTrainingDisplayContext(SingleModeLegendLoadResponse response)
{
    var loadCommon = response.data.single_mode_load_common;
    var data = new LegendScenarioResponseData(
        response,
        loadCommon.chara_info,
        loadCommon.home_info,
        response.data.legend_data_set,
        loadCommon.unchecked_event_array,
        loadCommon.race_start_info);
    var turn = new TurnInfoLegend(data);
    var trainStats = LegendTrainingStatsCalculator.CreateTrainStats(turn);
    return new(data, turn, trainStats, previousTurn: 1);
}

static SingleModeChara CreateChara(
    int playingState,
    int supportCardId = 30001,
    int friendship = 80,
    int singleModeCharaId = 0,
    int turn = 2)
    => new()
    {
        single_mode_chara_id = singleModeCharaId,
        speed = 100,
        stamina = 110,
        power = 120,
        guts = 130,
        wiz = 140,
        max_speed = 1500,
        max_stamina = 1500,
        max_power = 1500,
        max_guts = 1500,
        max_wiz = 1500,
        vital = 80,
        max_vital = 100,
        motivation = 5,
        turn = turn,
        skill_point = 100,
        state = 1,
        playing_state = playingState,
        support_card_array = [new() { position = 1, support_card_id = supportCardId }],
        evaluation_info_array = [new() { target_id = 1, evaluation = friendship }],
        training_level_info_array = [.. BaseTrainIds().Select(commandId => new TrainingLevelInfo { command_id = commandId, level = 1 })],
        chara_effect_id_array = []
    };

static SingleModeCommandInfo[] BaseTrainingCommands(int trainingPartnerCommandId = 101)
    => [.. BaseTrainIds().Select((commandId, index) => new SingleModeCommandInfo
    {
        command_type = 1,
        command_id = commandId,
        is_enable = 1,
        training_partner_array = commandId == trainingPartnerCommandId ? [1] : [],
        tips_event_partner_array = [],
        params_inc_dec_info_array = TrainingParams(index, includeVital: true),
        failure_rate = index * 5,
        sub_command_partner_array = []
    })];

static SingleModeLegendDataSet CreateLegendDataSet(int[]? obtainableBuffIds = null)
    => new()
    {
        command_info_array = [.. BaseTrainIds().Select((commandId, index) => new SingleModeLegendCommandInfo
        {
            command_type = 1,
            command_id = commandId,
            legend_id = 9046 + (index % 3),
            gain_gauge = index + 1,
            params_inc_dec_info_array = TrainingParams(index + 10, includeVital: false),
            friend_gauge_gain_array = []
        })],
        evaluation_info_array = [],
        gauge_count_array =
        [
            new() { legend_id = 9046, count = 2 },
            new() { legend_id = 9047, count = 4 },
            new() { legend_id = 9048, count = 6 },
        ],
        buff_info_array =
        [
            new() { buff_id = 1001, is_active = 1 },
            new() { buff_id = 2001, is_active = 1 },
            new() { buff_id = 3001, is_active = 0 },
        ],
        obtainable_buff_id_array = obtainableBuffIds ?? [1001],
        masterly_bonus_info = new(),
        race_history_array = [],
        activated_buff_id_array = []
    };

static SingleModeParamsIncDecInfo[] TrainingParams(int seed, bool includeVital)
{
    var targetTypes = includeVital ? new[] { 1, 2, 3, 4, 5, 30, 10 } : new[] { 1, 2, 3, 4, 5, 30 };
    return [.. targetTypes.Select((targetType, index) => new SingleModeParamsIncDecInfo
    {
        target_type = targetType,
        value = targetType == 10 ? -10 : seed + index + 1
    })];
}

static int[] BaseTrainIds() => [101, 105, 102, 103, 106];

static void WriteLegendBuffCsv()
{
    var dataDirectory = Path.Combine("PluginData", "LegendScenarioAnalyzer");
    Directory.CreateDirectory(dataDirectory);
    File.WriteAllText(
        Path.Combine(dataDirectory, "legend_buff.csv"),
        """
        cn_effect,name,rank,color,condition,buffId,isTrigger,isPerson,youQing,ganJing,xunLian,hintLv,hintCount,deYiLv,buZaiLv,jiBan,vitalCostDrop,fenShen,mood,note
        测试蓝效果,测试蓝心得,1,0,201,1001,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        测试蓝2效果,测试蓝2心得,2,0,201,1002,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实蓝1效果,真实蓝1心得,1,0,201,1101,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实蓝2效果,真实蓝2心得,2,0,201,1102,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实蓝4效果,真实蓝4心得,4,0,201,1104,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        测试绿效果,测试绿心得,1,1,201,2001,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        测试绿2效果,测试绿2心得,2,1,201,2002,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实绿1效果,真实绿1心得,1,1,201,2101,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实绿2效果,真实绿2心得,2,1,201,2102,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实绿4效果,真实绿4心得,4,1,201,2104,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        测试红效果,测试红心得,1,2,201,3001,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        测试红2效果,测试红2心得,2,2,201,3002,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实红1效果,真实红1心得,1,2,201,3101,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实红3效果,真实红3心得,3,2,201,3103,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        真实红4效果,真实红4心得,4,2,201,3104,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
        """);
}

static void EnsureHostConfigInitialized() => UraConfig.Initialize();

static async Task InitializeSmokeDatabase(IEnumerable<BaseName> names)
{
    WriteBrotliJson(UraDatabase.EVENT_NAME_FILEPATH, new List<Story>());
    WriteNamesBrotli(UraDatabase.NAMES_FILEPATH, names);
    WriteBrotliJson(UraDatabase.SKILLS_FILEPATH, new List<UmamusumeResponseAnalyzer.Entities.SkillData>());
    WriteBrotliJson(UraDatabase.SKILL_UPGRADE_SPECIALITY_FILEPATH, new List<SkillUpgradeSpeciality>());
    WriteBrotliJson(UraDatabase.TALENT_SKILLS_FILEPATH, new Dictionary<int, TalentSkillData[]>());
    WriteBrotliJson(UraDatabase.FACTOR_IDS_FILEPATH, new Dictionary<int, string>());
    WriteBrotliJson(UraDatabase.SADDLE_IDS_FILEPATH, Array.Empty<int>());
    WriteBrotliJson(UraDatabase.SUCCESSION_RELATION_FILEPATH, new SuccessionRelationTable());
    if (await UraDatabase.Initialize() != UmamusumeResponseAnalyzer.DatabaseAvailability.Ready)
        throw new InvalidOperationException("Legend smoke database fixture did not load atomically.");
}

static void WriteBrotliJson<T>(string path, T value)
{
    using var file = File.Create(path);
    using var brotli = new BrotliStream(file, CompressionLevel.SmallestSize);
    using var writer = new StreamWriter(brotli, Encoding.UTF8);
    using var json = new JsonTextWriter(writer);
    Newtonsoft.Json.JsonSerializer.CreateDefault().Serialize(json, value);
}

static void WriteNamesBrotli(string path, IEnumerable<BaseName> names)
{
    using var file = File.Create(path);
    using var brotli = new BrotliStream(file, CompressionLevel.SmallestSize);
    using var writer = new StreamWriter(brotli, Encoding.UTF8);
    using var json = new JsonTextWriter(writer);
    json.WriteStartArray();
    foreach (var name in names)
    {
        json.WriteStartObject();
        if (name is SupportCardName supportCard)
        {
            json.WritePropertyName("$type");
            json.WriteValue("UmamusumeResponseAnalyzer.Entities.SupportCardName, UmamusumeResponseAnalyzer");
            json.WritePropertyName(nameof(SupportCardName.CharaId));
            json.WriteValue(supportCard.CharaId);
            json.WritePropertyName(nameof(SupportCardName.Type));
            json.WriteValue(supportCard.Type);
        }
        json.WritePropertyName(nameof(BaseName.Id));
        json.WriteValue(name.Id);
        json.WritePropertyName(nameof(BaseName.Name));
        json.WriteValue(name.Name);
        json.WritePropertyName(nameof(BaseName.Nickname));
        json.WriteValue(name.Nickname);
        json.WriteEndObject();
    }
    json.WriteEndArray();
}

static void RequireFreshViewFactory(WorkspaceContent content)
{
    using var application = Terminal.Gui.App.Application.Create(new VirtualTimeProvider())
        .Init(DriverRegistry.Names.ANSI);
    using var first = content.CreateView();
    using var second = content.CreateView();
    if (ReferenceEquals(first, second))
        throw new InvalidOperationException("Legend WorkspaceContent reused a mounted Terminal.Gui View.");
}

static DashboardCapture CaptureDashboard(
    WorkspaceContent content,
    int width = 120,
    int height = 36)
{
    using var application = Terminal.Gui.App.Application.Create(new VirtualTimeProvider())
        .Init(DriverRegistry.Names.ANSI);
    application.Driver!.SetScreenSize(width, height);
    using var window = new Window
    {
        Width = Dim.Fill(),
        Height = Dim.Fill(),
        BorderStyle = null
    };
    using var view = content.CreateView();
    view.Width = Dim.Fill();
    view.Height = Dim.Fill();
    window.Add(view);

    DashboardCapture? capture = null;
    Exception? callbackFailure = null;
    application.AddTimeout(TimeSpan.Zero, () =>
    {
        try
        {
            application.Driver!.SetScreenSize(width, height);
            application.LayoutAndDraw(forceRedraw: true);
            capture = Capture(application, view, width, height);
            application.RequestStop(window);
        }
        catch (Exception ex)
        {
            callbackFailure = ex;
            application.RequestStop(window);
        }
        return false;
    });
    RunApplication(application, window, nameof(CaptureDashboard));

    if (callbackFailure is not null)
        ExceptionDispatchInfo.Capture(callbackFailure).Throw();
    return capture ?? throw new InvalidOperationException("Legend framebuffer was not captured.");
}

static DashboardResizeCapture CaptureResizeSequence(WorkspaceContent content)
{
    using var application = Terminal.Gui.App.Application.Create(new VirtualTimeProvider())
        .Init(DriverRegistry.Names.ANSI);
    using var window = new Window
    {
        Width = Dim.Fill(),
        Height = Dim.Fill(),
        BorderStyle = null
    };
    using var view = content.CreateView();
    view.Width = Dim.Fill();
    view.Height = Dim.Fill();
    window.Add(view);

    DashboardResizeCapture? capture = null;
    Exception? callbackFailure = null;
    application.AddTimeout(TimeSpan.Zero, () =>
    {
        try
        {
            application.Driver!.SetScreenSize(120, 36);
            application.LayoutAndDraw(forceRedraw: true);
            var wide = Capture(application, view, 120, 36);

            application.Driver.SetScreenSize(80, 30);
            application.LayoutAndDraw(forceRedraw: true);
            var narrow = Capture(application, view, 80, 30);

            view.ScrollHorizontal(view.GetContentSize().Width);
            application.LayoutAndDraw(forceRedraw: true);
            var narrowScrolled = Capture(application, view, 80, 30);

            application.Driver.SetScreenSize(120, 36);
            application.LayoutAndDraw(forceRedraw: true);
            var restored = Capture(application, view, 120, 36);

            capture = new(wide, narrow, narrowScrolled, restored);
            application.RequestStop(window);
        }
        catch (Exception ex)
        {
            callbackFailure = ex;
            application.RequestStop(window);
        }
        return false;
    });
    RunApplication(application, window, nameof(CaptureResizeSequence));

    if (callbackFailure is not null)
        ExceptionDispatchInfo.Capture(callbackFailure).Throw();
    return capture ?? throw new InvalidOperationException("Legend resize sequence was not captured.");
}

static DashboardVerticalScrollCapture CaptureVerticalScrollSequence(WorkspaceContent content)
{
    using var application = Terminal.Gui.App.Application.Create(new VirtualTimeProvider())
        .Init(DriverRegistry.Names.ANSI);
    using var window = new Window
    {
        Width = Dim.Fill(),
        Height = Dim.Fill(),
        BorderStyle = null
    };
    using var view = content.CreateView();
    view.Width = Dim.Fill();
    view.Height = Dim.Fill();
    window.Add(view);

    DashboardVerticalScrollCapture? capture = null;
    Exception? callbackFailure = null;
    application.AddTimeout(TimeSpan.Zero, () =>
    {
        try
        {
            application.Driver!.SetScreenSize(120, 36);
            application.LayoutAndDraw(forceRedraw: true);
            var initial = Capture(application, view, 120, 36);

            if (!LegendTrainingDisplayRenderer.TryScroll(view, Command.PageDown))
                throw new InvalidOperationException("Legend dashboard rejected PageDown.");
            application.LayoutAndDraw(forceRedraw: true);
            var pageDown = Capture(application, view, 120, 36);

            if (!LegendTrainingDisplayRenderer.TryScroll(view, Command.End))
                throw new InvalidOperationException("Legend dashboard rejected End.");
            application.LayoutAndDraw(forceRedraw: true);
            capture = new(initial, pageDown, Capture(application, view, 120, 36));
            application.RequestStop(window);
        }
        catch (Exception ex)
        {
            callbackFailure = ex;
            application.RequestStop(window);
        }
        return false;
    });
    RunApplication(application, window, nameof(CaptureVerticalScrollSequence));

    if (callbackFailure is not null)
        ExceptionDispatchInfo.Capture(callbackFailure).Throw();
    return capture ?? throw new InvalidOperationException("Legend vertical-scroll sequence was not captured.");
}

static void RunApplication(IApplication application, Window window, string helper)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
        application.RunAsync(window, timeout.Token).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
    {
        throw new TimeoutException($"{helper} timed out after 10 seconds.", ex);
    }
    if (timeout.IsCancellationRequested)
        throw new TimeoutException($"{helper} timed out after 10 seconds.");
}

static DashboardCapture Capture(IApplication application, View view, int width, int height)
{
    var cells = application.Driver!.Contents
        ?? throw new InvalidOperationException("Terminal.Gui framebuffer was not initialized.");
    return new(
        (Cell[,])cells.Clone(),
        new(0, 0, Math.Min(width, cells.GetLength(1)), Math.Min(height, cells.GetLength(0))),
        view.FrameToScreen(),
        view.Viewport,
        view.GetContentSize(),
        view.HorizontalScrollBar.Visible,
        view.HorizontalScrollBar.Value,
        view.VerticalScrollBar.Visible,
        view.VerticalScrollBar.Value);
}

static void RequireForeground(
    DashboardCapture capture,
    string token,
    int graphemeIndex,
    TColor expected,
    string name)
{
    var point = FindCellToken(capture, token).Points[graphemeIndex];
    var attribute = capture.Cells[point.Y, point.X].Attribute
        ?? throw new InvalidOperationException($"{name} has no attribute.");
    RequireEqual(expected, attribute.Foreground, $"{name} foreground");
    RequireEqual(TColor.Black, attribute.Background, $"{name} background");
}

static void RequireForegroundAt(DashboardCapture capture, Point point, TColor expected, string name)
{
    var attribute = capture.Cells[point.Y, point.X].Attribute
        ?? throw new InvalidOperationException($"{name} has no attribute.");
    RequireEqual(expected, attribute.Foreground, $"{name} foreground");
    RequireEqual(TColor.Black, attribute.Background, $"{name} background");
}

static void RequireBlackBackground(DashboardCapture capture, Point point, string name)
{
    var actual = capture.Cells[point.Y, point.X].Attribute?.Background
        ?? throw new InvalidOperationException($"{name} has no background attribute.");
    RequireEqual(TColor.Black, actual, $"{name} background");
}

static void RequireBorderForeground(DashboardCapture capture, Point point, TColor expected, string name)
    => RequireForegroundAt(capture, point, expected, name);

static CellToken FindCellToken(DashboardCapture capture, string token)
{
    var elements = new List<string>();
    var enumerator = StringInfo.GetTextElementEnumerator(token);
    while (enumerator.MoveNext())
        elements.Add((string)enumerator.Current);

    for (var y = capture.Region.Top; y < capture.Region.Bottom; y++)
    {
        for (var x = capture.Region.Left; x < capture.Region.Right; x++)
        {
            var points = new List<Point>(elements.Count);
            var column = x;
            var matches = true;
            foreach (var element in elements)
            {
                if (column >= capture.Region.Right || capture.Cells[y, column].Grapheme != element)
                {
                    matches = false;
                    break;
                }
                points.Add(new(column, y));
                column += Math.Max(1, element.GetColumns());
            }
            if (matches)
                return new(points);
        }
    }
    throw new InvalidOperationException($"Expected framebuffer token '{token}' was not found.");
}

static string CaptureWorkspace(WorkspaceSmokeSession ui, Workspace workspace)
{
    var current = Workspace.Current;
    workspace.SwitchTo();
    var screen = ui.CaptureScreen(200, 100);
    current?.SwitchTo();
    return screen;
}

static void RequireHistoryDisplay(
    WorkspaceSmokeSession ui,
    Workspace workspace,
    string expected,
    params string[] unexpected)
{
    if (!ReferenceEquals(Workspace.Current, workspace))
        workspace.SwitchTo();
    var rendered = ui.CaptureScreen(200, 100);
    if (!rendered.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"History display does not contain '{expected}'.");
    foreach (var value in unexpected)
    {
        if (rendered.Contains(value, StringComparison.Ordinal))
            throw new InvalidOperationException($"History display unexpectedly contains '{value}'.");
    }
}

static T Require<T>(T? value, string name)
    where T : class
    => value ?? throw new InvalidOperationException($"{name} was not found.");

static void RequireEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}.");
}

static string NormalizeNotificationCountdown(string rendered)
    => System.Text.RegularExpressions.Regex.Replace(rendered, @" \d+s ┘", " #s ┘");

static void RequireSequence<T>(IReadOnlyList<T> actual, IReadOnlyList<T> expected, string name)
{
    if (!actual.SequenceEqual(expected))
        throw new InvalidOperationException($"{name}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
}

static bool ContainsRenderedLine(string rendered, params string[] expectedParts)
    => rendered
        .Split(["\r\n", "\n"], StringSplitOptions.None)
        .Any(line => expectedParts.All(part => line.Contains(part, StringComparison.Ordinal)));

static int LineIndexContaining(string rendered, string text)
{
    var lines = rendered.Split(["\r\n", "\n"], StringSplitOptions.None);
    for (var i = 0; i < lines.Length; i++)
    {
        if (lines[i].Contains(text, StringComparison.Ordinal))
            return i;
    }

    return -1;
}

static int FirstColumnOfLineContaining(string rendered, string text)
{
    foreach (var line in rendered.Split(["\r\n", "\n"], StringSplitOptions.None))
    {
        var index = line.IndexOf(text, StringComparison.Ordinal);
        if (index >= 0)
            return index;
    }

    return -1;
}

sealed record DashboardCapture(
    Cell[,] Cells,
    Rectangle Region,
    Rectangle RootFrame,
    Rectangle RootViewport,
    Size RootContentSize,
    bool HorizontalScrollBarVisible,
    int HorizontalScrollBarValue,
    bool VerticalScrollBarVisible,
    int VerticalScrollBarValue)
{
    public string Text
    {
        get
        {
            var output = new StringBuilder();
            for (var y = Region.Top; y < Region.Bottom; y++)
            {
                var line = new StringBuilder();
                for (var x = Region.Left; x < Region.Right; x++)
                {
                    var grapheme = Cells[y, x].Grapheme;
                    line.Append(grapheme);
                    x += Math.Max(1, grapheme.GetColumns()) - 1;
                }
                output.AppendLine(line.ToString().TrimEnd());
            }
            return output.ToString().TrimEnd();
        }
    }
}

sealed record DashboardResizeCapture(
    DashboardCapture Wide,
    DashboardCapture Narrow,
    DashboardCapture NarrowScrolled,
    DashboardCapture Restored);

sealed record DashboardVerticalScrollCapture(
    DashboardCapture Initial,
    DashboardCapture PageDown,
    DashboardCapture Scrolled);

sealed record CellToken(IReadOnlyList<Point> Points);

sealed class HistoryLimitSettings : IDisposable
{
    readonly string path = Path.Combine("PluginData", "LegendScenarioAnalyzer", "settings.json");
    readonly byte[]? previous;

    public HistoryLimitSettings(int historyLimit)
    {
        previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            {
              "historyLimit": {{historyLimit}}
            }
            """);
    }

    public void Dispose()
    {
        if (previous is null)
            File.Delete(path);
        else
            File.WriteAllBytes(path, previous);
    }
}

sealed class RecordingPluginContext(IApplication application) : IPluginContext
{
    public IApplication Application => application;
    public IPluginHostEvents Events { get; } = new ThrowingPluginHostEvents();
    public RecordingPluginAnalyzerRegistry RecordedAnalyzers { get; } = new();
    public IPluginAnalyzerRegistry Analyzers => RecordedAnalyzers;
    public bool IsPluginAvailable(string internalName) => false;

    public void RunBackground(Func<CancellationToken, ValueTask> operation)
        => throw new NotSupportedException("Legend smoke does not use background operations.");
}

sealed class ThrowingPluginHostEvents : IPluginHostEvents
{
    public void OnStarted(Func<CancellationToken, ValueTask> handler)
        => throw new NotSupportedException("Legend smoke does not use host events.");
}

sealed class RecordingPluginAnalyzerRegistry : IPluginAnalyzerRegistry
{
    public List<AnalyzerRegistration> Registrations { get; } = [];

    public void Register<TPayload>(
        AnalyzerKind kind,
        IReadOnlyList<EndpointPattern> patterns,
        Func<AnalyzerInvocation<TPayload>, ValueTask> handler,
        int priority = 0)
        => Registrations.Add(new(typeof(TPayload), kind, [.. patterns], priority));
}

sealed record AnalyzerRegistration(
    Type PayloadType,
    AnalyzerKind Kind,
    IReadOnlyList<EndpointPattern> Patterns,
    int Priority);
