# LegendScenarioAnalyzer

`LegendScenarioAnalyzer` renders Legend scenario training and buff-selection information in a workspace. Each rendered analyzer response switches to the Legend workspace. The default Terminal.Gui display uses date/status panels, important information, Legend gauge panels, then either five horizontal training cards or aligned buff-selection rows, with Extras in a separate right column. Narrow windows keep the fixed layout available through horizontal and vertical scrolling.

The workspace keeps an in-memory history keyed by `single_mode_chara_id` and `turn`. Repeated output for the same key replaces that entry in place, including output rebuilt by `ModifyCurrent`. Use ↑/↓ for the previous/next entry and ←/→ for the oldest/newest entry; use PageUp/PageDown, Home/End, or the mouse wheel to scroll the current display.

`PluginData/LegendScenarioAnalyzer/settings.json` stores the history limit:

```json
{
  "historyLimit": 100
}
```

The valid range is `0` through `1000`. The default is `100`; `0` disables history and arrow-key navigation while continuing to show the latest analysis. History entries remain in memory only for the current plugin lifetime.

需要修改当前 Legend display 的插件在项目中引用本项目，并在自身 manifest `Dependencies` 中声明 `LegendScenarioAnalyzer`。依赖关系让两个插件共享 load context：

```csharp
using LegendScenarioAnalyzer;

var modified = LegendTrainingDisplay.ModifyCurrent((_, display) =>
{
    display.Training.Modify(LegendTrain.Speed, card =>
    {
        card.AddDescription("友情人数多时优先考虑");
        card.Highlight();
    });

    display.Selection.Modify(LegendBuffColor.Green, 1, card =>
    {
        card.AddText("优先选择");
    });
}, switchToWorkspace: false);
```

`ModifyCurrent` 从最新默认数据重建并发布一次修改；当前没有可修改的 display 时返回 `false`。它默认切换到 Legend workspace；传 `switchToWorkspace: false` 可静默刷新，传入 cancellation token 可阻止过期结果发布。每次 workspace mount 都创建新的 Terminal.Gui view，公开 display API 不暴露 `View` 实例。
