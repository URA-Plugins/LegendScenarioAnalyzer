# LegendScenarioAnalyzer

`LegendScenarioAnalyzer` renders Legend scenario training and buff-selection information in a workspace. Each rendered analyzer response switches to the Legend workspace. The default Terminal.Gui display uses date/status panels, important information, Legend gauge panels, then either five horizontal training cards or aligned buff-selection rows, with Extras in a separate right column. Narrow windows keep the fixed layout available through horizontal and vertical scrolling.

The workspace retains display IDs keyed by `(single_mode_chara_id, turn)`. Repeated analyzer output for the same ID updates that unit in place. Use ↑/↓ for the previous/next ID and ←/→ for the oldest/newest ID; use PageUp/PageDown, Home/End, or the mouse wheel to scroll the selected display.

`PluginData/LegendScenarioAnalyzer/settings.json` stores the history limit:

```json
{
  "historyLimit": 100
}
```

The valid range is `0` through `1000`. The default is `100`; `0` disables history and arrow-key navigation while continuing to show the latest analysis. History entries remain in memory only for the current plugin lifetime.

需要修改 Legend display 的插件在项目中引用本项目，并在自身 manifest `Dependencies` 中声明 `LegendScenarioAnalyzer`。目标插件存在时依赖关系让两个插件共享 load context；调用前通过 `IPluginContext.IsPluginAvailable("LegendScenarioAnalyzer")` 确认本轮可用。目标缺失时 Consumer 仍可独立加载。

```csharp
using LegendScenarioAnalyzer;

using var producer = LegendTrainingDisplay.RegisterPartProducer("MyPlugin");
var id = new LegendTrainingDisplayId(singleModeCharaId, turn);
producer.Update(id, (_, display) =>
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
    display.Extra.AddText("本插件的补充信息");
});
var shown = LegendTrainingDisplay.Show(id, switchToWorkspace: false);
```

`producer.Update(id, part)` 只替换该 producer 在指定 ID 下的 part，不触碰 View；同一 producer 对同一 ID 最后一次更新胜出。`Show(id)` 将该 ID 的场景基础 part 与所有 producer parts 组合并发布一次；场景 part 尚不存在或 cancellation 已请求时返回 `false`。`switchToWorkspace` 只控制这次 Show 是否切换 workspace。

不同 ID 的 parts 完全隔离；producer 注册顺序决定组合顺序。`RegisterPartProducer(sourceTitle)` 要求非空单行标题。Analyzer 自身的非空 Extra 以青色 `Legend` 标题开头，随后每个 producer 的非空 Extra 以其青色标题开头；各 section 连续显示且不插入内层边框、缩进或空行。producer 注册、Update 与 Dispose 均不隐式发布。`Important`、`Extra`、训练卡、选择卡和场景 panel 编辑器均支持普通文本及由 `LegendDisplaySegment` 组成的带颜色内容。part 抛出异常时保留最后一次成功显示。

## 构建

仓库通过 NuGet 包引用 Host API。克隆后在仓库根执行：

```powershell
dotnet build .\LegendScenarioAnalyzer.csproj -c Release -m:1 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:PlatformTarget=AnyCPU -p:DeployUraPluginToLocalAppDataOnBuild=false
```

Host-dependent smoke 位于 `tests/LegendScenarioAnalyzerSmoke`，测试进程在初始化 Host 配置前固定使用 `zh-CN`，布局断言使用中文文本与对应列宽。

## 验证与发布

在 Windows 仓库根执行 `act workflow_dispatch --artifact-server-path "$env:TEMP/ura-act-artifacts"`。本地与 GitHub 使用同一份 workflow；版本 tag 触发 GitHub Release 发布。环境要求、共用 workflow 本地映射和发布规则见 [URA plugin workflows](https://github.com/URA-Plugins/.github/blob/v1/README.md)。
