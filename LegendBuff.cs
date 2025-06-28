using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

namespace LegendScenarioAnalyzer
{
    /// <summary>
    /// 参照 资源文件 Legend -> legend_buff.csv
    /// </summary>
    public class LegendBuff
    {
        public string cn_effect { get; set; }
        public string name { get; set; }
        [TypeConverter(typeof(IntNodeConverter))]
        public int rank { get; set; }    // 星数
        [TypeConverter(typeof(IntNodeConverter))]
        public int color { get; set; }
        [TypeConverter(typeof(IntNodeConverter))]
        public int condition { get; set; }       // 触发条件 (LegendBuffCondition)
        [TypeConverter(typeof(IntNodeConverter))]
        public int buffId { get; set; }
        [TypeConverter(typeof(BooleanNodeConverter))]
        public bool isTrigger { get; set; }      // 是否为触发后的效果
        [TypeConverter(typeof(BooleanNodeConverter))]
        public bool isPerson { get; set; }       // 是否为人头效果
        [TypeConverter(typeof(IntNodeConverter))]
        public int youQing { get; set; }     // 友情
        [TypeConverter(typeof(IntNodeConverter))]
        public int ganJing { get; set; }     // 干劲
        [TypeConverter(typeof(IntNodeConverter))]
        public int xunLian { get; set; }     // 训练
        [TypeConverter(typeof(IntNodeConverter))]
        public int hintLv { get; set; }      // Hint率
        [TypeConverter(typeof(IntNodeConverter))]
        public int hintCount { get; set; }   // Hint数量
        [TypeConverter(typeof(IntNodeConverter))]
        public int deYiLv { get; set; }      // 得意率
        [TypeConverter(typeof(IntNodeConverter))]
        public int buZaiLv { get; set; }     // 不在率
        [TypeConverter(typeof(IntNodeConverter))]
        public int jiBan { get; set; }     // 羁绊
        [TypeConverter(typeof(IntNodeConverter))]
        public int vitalCostDrop { get; set; } // 减体力消耗
        [TypeConverter(typeof(IntNodeConverter))]
        public int fenShen { get; set; }  // 分身
        [TypeConverter(typeof(IntNodeConverter))]
        public int mood { get; set; }       // 心情        
        public string note { get; set; }
    }

    /// <summary>
    /// 对应 single_mode_10_buff.condition_group_id
    /// </summary>
    public enum LegendBuffCondition
    {
        Training = 201,     // 训练
        Rest = 202,         // 休息
        Friendship = 801,   // 彩圈
        Person3 = 1301,     // 3人头
        Person5 = 1302,     // 5人头（国）
        Mood = 1401         // 绝好调
    }

    public class BooleanNodeConverter : DefaultTypeConverter
    {
        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (text == null)
                return false;
            else
                return text.ToLower() == "true";
        }
    }

    public class IntNodeConverter : DefaultTypeConverter
    {
        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (text == null || text == "")
                return 0;
            else
                return Int32.Parse(text);
        }
    }

}