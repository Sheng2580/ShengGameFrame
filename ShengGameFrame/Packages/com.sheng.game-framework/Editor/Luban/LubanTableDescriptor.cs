using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// 单个 C# 配置字段的 Luban 列描述
    /// </summary>
    public sealed class LubanColumnDescriptor
    {
        public string Name;
        public string FormerName;
        public string LubanType;
        public string Comment;
        public string DefaultValue;
        public bool Required;
        public bool IsKey;
        public bool IsEnum;
        public Type EnumType;
        public MemberInfo Member;
    }

    /// <summary>
    /// 单个 C# 配置类的 Luban 表描述
    /// </summary>
    public sealed class LubanTableDescriptor
    {
        public Type ModelType;
        public string TableName;
        public string TableTypeName;
        public string ValueTypeName;
        public string OutputName;
        public string Group;
        public string Comment;
        public string FileName;
        public LubanColumnDescriptor KeyColumn;
        public readonly List<LubanColumnDescriptor> Columns =
            new List<LubanColumnDescriptor>();
    }

    /// <summary>
    /// C# 配置类扫描结果
    /// </summary>
    public sealed class LubanScanResult
    {
        public readonly List<LubanTableDescriptor> Tables =
            new List<LubanTableDescriptor>();
        public readonly List<string> Errors = new List<string>();
        public bool Success => Errors.Count == 0;
    }

    /// <summary>
    /// 表结构同步结果
    /// </summary>
    public sealed class LubanTableSyncResult
    {
        public bool Success;
        public bool RequiresRemovalConfirmation;
        public string TableName;
        public string FilePath;
        public readonly List<string> AddedColumns = new List<string>();
        public readonly List<string> RemovedColumns = new List<string>();
        public readonly List<string> Errors = new List<string>();
    }
}
