using CsvHelper;
using System.Collections.Frozen;
using System.Globalization;

namespace LegendScenarioAnalyzer
{
    public static class GameGlobal
    {
        public static readonly Dictionary<int, int[]> FiveStatusLimit = new Dictionary<int, int[]>
        {
            { 6, [ 2000, 2000, 1800, 1800, 1400] },
            { 7, [ 2200, 1800, 1800, 1800, 1400] },
            { 8, [ 2300, 1000, 2200, 2200, 1500] },
            { 9, [ 2300, 2200, 1800, 1400, 1400] },
            { 10, [ 2500, 2000, 2000, 1800, 1700] }
        }; 
        public static readonly FrozenDictionary<int, int> XiahesuIds = new Dictionary<int, int>
        {
            { 101, 601 },
            { 105, 602 },
            { 102, 603 },
            { 103, 604 },
            { 106, 605 }
        }.ToFrozenDictionary();
        public static readonly FrozenDictionary<int, int> ToTrainId = new Dictionary<int, int>
        {
            [1101] = 101,
            [1102] = 105,
            [1103] = 102,
            [1104] = 103,
            [1105] = 106,
            [601] = 101,
            [602] = 105,
            [603] = 102,
            [604] = 103,
            [605] = 106,
            [101] = 101,
            [105] = 105,
            [102] = 102,
            [103] = 103,
            [106] = 106,
            [2101] = 101,
            [2201] = 101,
            [2301] = 101,
            [2102] = 105,
            [2202] = 105,
            [2302] = 105,
            [2103] = 102,
            [2203] = 102,
            [2303] = 102,
            [2104] = 103,
            [2204] = 103,
            [2304] = 103,
            [2105] = 106,
            [2205] = 106,
            [2305] = 106,
            [901] = 101,
            [902] = 102,
            [906] = 106
        }.ToFrozenDictionary();
        public static readonly int[] TrainIds = [101, 105, 102, 103, 106];
        public static readonly FrozenDictionary<int, int> ToTrainIndex = new Dictionary<int, int>
        {
            { 1101, 0 },
            { 1102, 1 },
            { 1103, 2 },
            { 1104, 3 },
            { 1105, 4 },
            { 601, 0 },
            { 602, 1 },
            { 603, 2 },
            { 604, 3 },
            { 605, 4 },
            { 101, 0 },
            { 105, 1 },
            { 102, 2 },
            { 103, 3 },
            { 106, 4 },
            { 2101, 0 },
            { 2201, 0 },
            { 2301, 0 },
            { 2102, 1 },
            { 2202, 1 },
            { 2302, 1 },
            { 2103, 2 },
            { 2203, 2 },
            { 2303, 2 },
            { 2104, 3 },
            { 2204, 3 },
            { 2304, 3 },
            { 2105, 4 },
            { 2205, 4 },
            { 2305, 4 },
            { 901, 0 },
            { 902, 2 },
            { 906, 4 }
        }.ToFrozenDictionary();
        public static readonly FrozenDictionary<int, int> LegendBuffShortId = new Dictionary<int, int>
        {
            { 1101, 0 },
            { 1102, 1 },
            { 1103, 2 },
            { 1104, 3 },
            { 1201, 4 },
            { 1202, 5 },
            { 1203, 6 },
            { 1204, 7 },
            { 1205, 8 },
            { 1206, 9 },
            { 1301, 10 },
            { 1302, 11 },
            { 1303, 12 },
            { 1304, 13 },
            { 1305, 14 },
            { 1306, 15 },
            { 1307, 16 },
            { 1308, 17 },
            { 1309, 18 },
            { 2101, 19 },
            { 2102, 20 },
            { 2103, 21 },
            { 2104, 22 },
            { 2201, 23 },
            { 2202, 24 },
            { 2203, 25 },
            { 2204, 26 },
            { 2205, 27 },
            { 2206, 28 },
            { 2301, 29 },
            { 2302, 30 },
            { 2303, 31 },
            { 2304, 32 },
            { 2305, 33 },
            { 2306, 34 },
            { 2307, 35 },
            { 2308, 36 },
            { 2309, 37 },
            { 3101, 38 },
            { 3102, 39 },
            { 3103, 40 },
            { 3104, 41 },
            { 3201, 42 },
            { 3202, 43 },
            { 3203, 44 },
            { 3204, 45 },
            { 3205, 46 },
            { 3206, 47 },
            { 3301, 48 },
            { 3302, 49 },
            { 3303, 50 },
            { 3304, 51 },
            { 3305, 52 },
            { 3306, 53 },
            { 3307, 54 },
            { 3308, 55 },
            { 3309, 56 }
        }.ToFrozenDictionary();
        public static List<LegendBuff> LegendBuffInfo = [];

        public static void LoadLegendBuffs()
        {
            var dataDirectory = Path.Combine("PluginData", "LegendScenarioAnalyzer");
            Directory.CreateDirectory(dataDirectory);
            var path = Path.Combine(dataDirectory, "legend_buff.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"缺少传奇杯心得数据文件，请将 legend_buff.csv 放到 {Path.GetFullPath(dataDirectory)}。", path);

            using var ms = new MemoryStream(File.ReadAllBytes(path));
            using var reader = new StreamReader(ms);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            LegendBuffInfo = csv.GetRecords<LegendBuff>().ToList();
        }
        public static class ScoreUtils
        {
            public static double ScoreOfVital(int vital, int maxVital)
            {
                //四段折线
                if (vital <= 50)
                    return 2.5 * vital;
                else if (vital <= 75)
                    return 1.7 * (vital - 50) + ScoreOfVital(50, maxVital);
                else if (vital <= maxVital - 10)
                    return 1.2 * (vital - 75) + ScoreOfVital(75, maxVital);
                else
                    return 0.7 * (vital - (maxVital - 10)) + ScoreOfVital(maxVital - 10, maxVital);
            }
            public static int ReviseOver1200(int x)
            {
                return x > 1200 ? x * 2 - 1200 : x;
            }

        }
    }
}
