using Newtonsoft.Json.Linq;
using Spectre.Console;
using System.Diagnostics;
using System.IO.Compression;
using UmamusumeResponseAnalyzer;
using UmamusumeResponseAnalyzer.Plugin;

namespace LegendScenarioAnalyzer
{
    public class LegendScenarioAnalyzer : IPlugin
    {
        public Version Version => new(1, 0, 0);

        public string Name => "LegendScenarioAnalyzer";

        public string Author => "UmaAi Team";
        public async Task UpdatePlugin(ProgressContext ctx)
        {
            var progress = ctx.AddTask($"[LegendScenarioAnalyzer] Update");

            using var client = new HttpClient();
            using var resp = await client.GetAsync($"https://api.github.com/repos/URA-Plugins/{Name}/releases/latest");
            var json = await resp.Content.ReadAsStringAsync();
            var jo = JObject.Parse(json);

            var isLatest = ("v" + Version.ToString()).Equals("v" + jo["tag_name"]?.ToString());
            if (isLatest)
            {
                progress.Increment(progress.MaxValue);
                progress.StopTask();
                return;
            }
            progress.Increment(25);

            using var msg = await client.GetAsync(jo["assets"][0]["browser_download_url"].ToString(), HttpCompletionOption.ResponseHeadersRead);
            using var stream = await msg.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                    break;
                progress.Increment(read / msg.Content.Headers.ContentLength ?? 1 * 0.5);
            }
            using var archive = new ZipArchive(stream);
            archive.ExtractToDirectory(Path.Combine("Plugins", Name), true);
            progress.Increment(25);

            progress.StopTask();
        }

        public void Initialize()
        {
            Trace.WriteLine(Thread.CurrentThread.CurrentCulture);
            Trace.WriteLine(Thread.CurrentThread.CurrentUICulture);
            i18n.Game.Culture = Thread.CurrentThread.CurrentCulture;
            Trace.WriteLine(i18n.Game.I18N_Speed);
            Trace.WriteLine(i18n.Game.Culture);
            Trace.WriteLine(i18n.Game.ResourceManager.GetString("I18N_Grass",Thread.CurrentThread.CurrentCulture));
        }
        [Analyzer(priority: 1)]
        public static void Analyze(JObject jo)
        {
            if (!jo.HasCharaInfo()) return;
            if (jo["data"] is null || jo["data"] is not JObject data) return;
            if (data["chara_info"] is null || data["chara_info"] is not JObject chara_info) return;
            if (chara_info["scenario_id"].ToInt() != 10) return;
            var state = chara_info["state"].ToInt();
            if (chara_info != null && data["home_info"]?["command_info_array"] != null && data["race_reward_info"].IsNull() && !(state is 2 or 3)) //根据文本简单过滤防止重复、异常输出
            {
                var @event = jo.ToObject<Gallop.SingleModeCheckEventResponse>();
                if ((@event.data.unchecked_event_array != null && @event.data.unchecked_event_array.Length > 0) || @event.data.race_start_info != null) return;
                LegendHandler.ParseLegendCommandInfo(@event);
            }
        }
    }
}
