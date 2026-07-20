using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sheng.GameFramework.Config;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// 将带特性的 C# 配置类转换为 Luban 表描述
    /// </summary>
    public static class LubanTableScanner
    {
        private static readonly Dictionary<Type, string> ScalarTypeMappings =
            new Dictionary<Type, string>
            {
                { typeof(bool), "bool" },
                { typeof(byte), "byte" },
                { typeof(sbyte), "sbyte" },
                { typeof(short), "short" },
                { typeof(ushort), "ushort" },
                { typeof(int), "int" },
                { typeof(uint), "uint" },
                { typeof(long), "long" },
                { typeof(ulong), "ulong" },
                { typeof(float), "float" },
                { typeof(double), "double" },
                { typeof(string), "string" }
            };

        public static LubanScanResult ScanProject()
        {
            return ScanTypes(TypeCache
                .GetTypesWithAttribute<LubanTableAttribute>()
                .Where(type => !IsTestAssembly(type.Assembly)));
        }

        public static LubanScanResult ScanTypes(IEnumerable<Type> types)
        {
            LubanScanResult result = new LubanScanResult();
            if (types == null)
            {
                result.Errors.Add("配置类型集合为空");
                return result;
            }

            HashSet<string> tableNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> outputNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (Type type in types.OrderBy(
                         value => value.FullName,
                         StringComparer.Ordinal))
            {
                if (!TryCreateDescriptor(type, out LubanTableDescriptor table, out string error))
                {
                    result.Errors.Add(error);
                    continue;
                }

                if (!tableNames.Add(table.TableName))
                {
                    result.Errors.Add($"Luban 表名重复 {table.TableName}");
                    continue;
                }

                if (!outputNames.Add(table.OutputName))
                {
                    result.Errors.Add($"Luban JSON 输出名重复 {table.OutputName}");
                    continue;
                }

                result.Tables.Add(table);
            }

            return result;
        }

        public static bool TryCreateDescriptor(
            Type type,
            out LubanTableDescriptor table,
            out string error)
        {
            table = null;
            error = string.Empty;
            LubanTableAttribute tableAttribute =
                type?.GetCustomAttribute<LubanTableAttribute>();
            if (type == null || tableAttribute == null)
            {
                error = "类型没有 LubanTable 特性";
                return false;
            }

            if (!type.IsClass || type.IsAbstract || type.IsGenericType
                              || typeof(MonoBehaviour).IsAssignableFrom(type)
                              || typeof(ScriptableObject).IsAssignableFrom(type))
            {
                error = $"{type.FullName} 必须是普通非抽象 C# 类";
                return false;
            }

            string tableName = NormalizeIdentifier(tableAttribute.TableName);
            if (string.IsNullOrEmpty(tableName))
            {
                error = $"{type.FullName} 的 Luban 表名无效";
                return false;
            }

            table = new LubanTableDescriptor
            {
                ModelType = type,
                TableName = tableName,
                TableTypeName = "Tb" + ToPascalCase(tableName),
                ValueTypeName = type.Name,
                OutputName = NormalizeOutputName(
                    tableAttribute.OutputName,
                    tableName),
                Group = string.IsNullOrWhiteSpace(tableAttribute.Group)
                    ? "c"
                    : tableAttribute.Group.Trim(),
                Comment = tableAttribute.Comment ?? string.Empty,
                FileName = "#" + tableName + ".xlsx"
            };
            if (string.IsNullOrEmpty(table.OutputName))
            {
                error = $"{type.FullName} 的 Luban JSON 输出名无效";
                table = null;
                return false;
            }

            MemberInfo[] members = type
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsSupportedMember)
                .OrderBy(member => member.MetadataToken)
                .ToArray();
            HashSet<string> columnNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> formerNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < members.Length; i++)
            {
                MemberInfo member = members[i];
                if (member.GetCustomAttribute<LubanIgnoreAttribute>() != null)
                {
                    continue;
                }

                Type memberType = GetMemberType(member);
                if (!TryMapType(memberType, out string lubanType, out string typeError))
                {
                    error = $"{type.FullName}.{member.Name} {typeError}";
                    return false;
                }

                LubanColumnAttribute columnAttribute =
                    member.GetCustomAttribute<LubanColumnAttribute>();
                if (lubanType.StartsWith("list,", StringComparison.Ordinal))
                {
                    string separator = columnAttribute?.Separator ?? ",";
                    if (string.IsNullOrEmpty(separator)
                        || separator.IndexOfAny(
                            new[] { '#', '&', '\r', '\n', '(', ')', '[', ']', '{', '}' }) >= 0)
                    {
                        error = $"{type.FullName}.{member.Name} 的集合分隔符无效";
                        return false;
                    }

                    string encodedSeparator = separator.IndexOfAny(
                        new[] { ',', ';' }) >= 0
                            ? "(" + separator + ")"
                            : separator;
                    lubanType = "list#sep="
                                + encodedSeparator
                                + ","
                                + lubanType.Substring("list,".Length);
                }

                string columnName = string.IsNullOrWhiteSpace(columnAttribute?.Name)
                    ? ToCamelCase(member.Name)
                    : columnAttribute.Name.Trim();
                if (!IsValidIdentifier(columnName))
                {
                    error = $"{type.FullName} 的字段列名无效 {columnName}";
                    return false;
                }

                if (!columnNames.Add(columnName))
                {
                    error = $"{type.FullName} 的字段列名重复 {columnName}";
                    return false;
                }

                string formerName = columnAttribute?.FormerName?.Trim();
                if (!string.IsNullOrEmpty(formerName)
                    && !IsValidIdentifier(formerName))
                {
                    error = $"{type.FullName} 的旧字段列名无效 {formerName}";
                    return false;
                }

                if (!string.IsNullOrEmpty(formerName)
                    && !formerNames.Add(formerName))
                {
                    error = $"{type.FullName} 的旧字段列名重复 {formerName}";
                    return false;
                }

                Type enumType = ResolveEnumType(memberType);
                LubanColumnDescriptor column = new LubanColumnDescriptor
                {
                    Name = columnName,
                    FormerName = formerName,
                    LubanType = lubanType,
                    Comment = columnAttribute?.Comment ?? string.Empty,
                    DefaultValue = columnAttribute?.DefaultValue ?? string.Empty,
                    Required = columnAttribute?.Required ?? true,
                    IsKey = member.GetCustomAttribute<LubanKeyAttribute>() != null,
                    IsEnum = enumType != null,
                    EnumType = enumType,
                    Member = member
                };
                table.Columns.Add(column);
                if (column.IsKey)
                {
                    if (table.KeyColumn != null)
                    {
                        error = $"{type.FullName} 只能声明一个 LubanKey";
                        return false;
                    }

                    table.KeyColumn = column;
                }
            }

            if (table.Columns.Count == 0)
            {
                error = $"{type.FullName} 没有可生成的公开字段";
                return false;
            }

            if (table.KeyColumn == null)
            {
                error = $"{type.FullName} 必须声明一个 LubanKey";
                return false;
            }

            if (table.KeyColumn.LubanType.StartsWith("list", StringComparison.Ordinal)
                || table.KeyColumn.LubanType.EndsWith("?", StringComparison.Ordinal))
            {
                error = $"{type.FullName} 的 LubanKey 必须是非空标量";
                return false;
            }

            for (int i = 0; i < table.Columns.Count; i++)
            {
                LubanColumnDescriptor column = table.Columns[i];
                if (!string.IsNullOrEmpty(column.FormerName)
                    && !string.Equals(
                        column.FormerName,
                        column.Name,
                        StringComparison.OrdinalIgnoreCase)
                    && columnNames.Contains(column.FormerName))
                {
                    error = $"{type.FullName} 的旧字段名 {column.FormerName} 与当前字段冲突";
                    return false;
                }
            }

            return true;
        }

        private static bool IsTestAssembly(Assembly assembly)
        {
            string assemblyName = assembly?.GetName().Name ?? string.Empty;
            return assemblyName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
                   || assemblyName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedMember(MemberInfo member)
        {
            if (member is FieldInfo field)
            {
                return !field.IsStatic;
            }

            if (member is PropertyInfo property)
            {
                return property.GetIndexParameters().Length == 0
                       && property.GetMethod?.IsPublic == true
                       && property.SetMethod?.IsPublic == true;
            }

            return false;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member is FieldInfo field
                ? field.FieldType
                : ((PropertyInfo)member).PropertyType;
        }

        private static bool TryMapType(
            Type type,
            out string lubanType,
            out string error)
        {
            lubanType = string.Empty;
            error = string.Empty;
            Type nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                if (!TryMapScalarType(nullableType, out string nullableScalar))
                {
                    error = $"不支持可空类型 {type.Name}";
                    return false;
                }

                lubanType = nullableScalar + "?";
                return true;
            }

            Type elementType = null;
            if (type.IsArray)
            {
                elementType = type.GetElementType();
            }
            else
            {
                TryGetListElementType(type, out elementType);
            }

            if (elementType != null)
            {
                if (Nullable.GetUnderlyingType(elementType) != null)
                {
                    error = $"集合元素不能使用可空类型 {elementType.Name}";
                    return false;
                }

                if (!TryMapScalarType(elementType, out string elementLubanType))
                {
                    error = $"不支持集合元素类型 {elementType.Name}";
                    return false;
                }

                lubanType = "list," + elementLubanType;
                return true;
            }

            if (TryMapScalarType(type, out lubanType))
            {
                return true;
            }

            error = $"不支持字段类型 {type.FullName}";
            return false;
        }

        private static bool TryMapScalarType(Type type, out string lubanType)
        {
            if (type.IsEnum)
            {
                lubanType = "string";
                return true;
            }

            return ScalarTypeMappings.TryGetValue(type, out lubanType);
        }

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (!type.IsGenericType)
            {
                return false;
            }

            Type genericDefinition = type.GetGenericTypeDefinition();
            if (genericDefinition != typeof(List<>)
                && genericDefinition != typeof(IList<>)
                && genericDefinition != typeof(IReadOnlyList<>))
            {
                return false;
            }

            elementType = type.GetGenericArguments()[0];
            return true;
        }

        private static Type ResolveEnumType(Type type)
        {
            Type resolvedType = Nullable.GetUnderlyingType(type) ?? type;
            if (resolvedType.IsArray)
            {
                resolvedType = resolvedType.GetElementType();
            }
            else if (TryGetListElementType(resolvedType, out Type elementType))
            {
                resolvedType = Nullable.GetUnderlyingType(elementType) ?? elementType;
            }

            return resolvedType?.IsEnum == true ? resolvedType : null;
        }

        private static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] characters = value.Trim().ToCharArray();
            bool containsLetterOrDigit = false;
            for (int i = 0; i < characters.Length; i++)
            {
                char character = characters[i];
                if (!char.IsLetterOrDigit(character) && character != '_')
                {
                    return string.Empty;
                }

                containsLetterOrDigit |= char.IsLetterOrDigit(character);
            }

            return containsLetterOrDigit
                ? new string(characters).ToLowerInvariant()
                : string.Empty;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!char.IsLetter(value[0]) && value[0] != '_')
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeOutputName(string value, string tableName)
        {
            string outputName = string.IsNullOrWhiteSpace(value)
                ? "tb" + tableName
                : value.Trim();
            if (outputName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                outputName = outputName.Substring(0, outputName.Length - 5);
            }

            return string.IsNullOrEmpty(NormalizeIdentifier(outputName))
                ? string.Empty
                : outputName.ToLowerInvariant();
        }

        private static string ToCamelCase(string value)
        {
            return string.IsNullOrEmpty(value)
                ? value
                : char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static string ToPascalCase(string value)
        {
            string[] segments = value.Split(
                new[] { '_' },
                StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(segments.Select(segment =>
                char.ToUpperInvariant(segment[0]) + segment.Substring(1)));
        }
    }
}
