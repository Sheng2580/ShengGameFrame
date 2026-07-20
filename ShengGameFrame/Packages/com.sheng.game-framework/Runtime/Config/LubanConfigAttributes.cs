using System;

namespace Sheng.GameFramework.Config
{
    /// <summary>
    /// 标记需要生成 Luban 表的配置类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LubanTableAttribute : Attribute
    {
        public LubanTableAttribute(string tableName)
        {
            TableName = tableName;
        }

        public string TableName { get; }
        public string OutputName { get; set; }
        public string Group { get; set; } = "c";
        public string Comment { get; set; }
    }

    /// <summary>
    /// 标记配置表主键字段
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LubanKeyAttribute : Attribute
    {
    }

    /// <summary>
    /// 配置字段的 Luban 列信息
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LubanColumnAttribute : Attribute
    {
        public LubanColumnAttribute()
        {
        }

        public LubanColumnAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string DefaultValue { get; set; }
        public string Comment { get; set; }
        public bool Required { get; set; } = true;
        public string FormerName { get; set; }
        public string Separator { get; set; } = ",";
    }

    /// <summary>
    /// 排除不需要写入配置表的运行时字段
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LubanIgnoreAttribute : Attribute
    {
    }

}
