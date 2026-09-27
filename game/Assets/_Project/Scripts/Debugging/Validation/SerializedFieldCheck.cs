using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GuildMaster.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Обход сериализуемых полей ассета через отражение — общие проверки для всех данных:
    /// пустые ссылки (кроме <see cref="OptionalReferenceAttribute"/>), числа вне <c>[Range]</c> / <c>[Min]</c>,
    /// диапазоны с min &gt; max, пустые строки в списках. В чужие ассеты не заходит — они проверяются сами.
    /// </summary>
    internal static class SerializedFieldCheck
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void Run(Object asset, DataValidationReport report)
        {
            if (asset == null) return;
            Walk(asset, asset, string.Empty, report);
        }

        private static void Walk(object target, Object asset, string path, DataValidationReport report)
        {
            for (Type type = target.GetType(); type != null && type != typeof(ScriptableObject) && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(InstanceFields))
                {
                    if (!IsSerialized(field)) continue;
                    string fieldPath = path.Length == 0 ? field.Name : path + "." + field.Name;
                    CheckValue(field, field.FieldType, field.GetValue(target), asset, fieldPath, report, inList: false);
                }
            }
        }

        private static void CheckValue(FieldInfo field, Type type, object value, Object asset, string path, DataValidationReport report, bool inList)
        {
            if (typeof(Object).IsAssignableFrom(type))
            {
                bool optional = !inList && field.GetCustomAttribute<OptionalReferenceAttribute>() != null;
                if (!optional && (value as Object) == null) report.Error(asset, path, "пустая ссылка");
                return;
            }

            if (type == typeof(string))
            {
                if (inList && string.IsNullOrWhiteSpace((string)value)) report.Error(asset, path, "пустая строка в списке");
                return;
            }

            if (type == typeof(int) || type == typeof(float))
            {
                CheckNumber(field, Convert.ToSingle(value), asset, path, report);
                return;
            }

            if (type == typeof(IntRange))
            {
                var range = (IntRange)value;
                if (range.Min > range.Max) report.Error(asset, path, $"диапазон {range}: min больше max");
                return;
            }

            if (type == typeof(FloatRange))
            {
                var range = (FloatRange)value;
                if (float.IsNaN(range.Min) || float.IsNaN(range.Max) || range.Min > range.Max)
                    report.Error(asset, path, $"диапазон {range}: min больше max");
                return;
            }

            if (value is IList list)
            {
                Type elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                for (int i = 0; i < list.Count; i++)
                {
                    CheckValue(field, elementType, list[i], asset, $"{path}[{i}]", report, inList: true);
                }
                return;
            }

            if (value != null && !type.IsPrimitive && !type.IsEnum && type.IsDefined(typeof(SerializableAttribute), false))
            {
                Walk(value, asset, path, report);
            }
        }

        private static void CheckNumber(FieldInfo field, float value, Object asset, string path, DataValidationReport report)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                report.Error(asset, path, "не число");
                return;
            }

            var range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null && (value < range.min || value > range.max))
                report.Error(asset, path, $"{value} вне допустимого {range.min}…{range.max}");

            var min = field.GetCustomAttribute<MinAttribute>();
            if (min != null && value < min.min)
                report.Error(asset, path, $"{value} меньше допустимого {min.min}");
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsNotSerialized) return false;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), false);
        }
    }
}
