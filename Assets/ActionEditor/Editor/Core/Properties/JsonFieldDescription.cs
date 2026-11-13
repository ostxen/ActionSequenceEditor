using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NBC.ActionEditor
{
    public sealed class JsonFieldDescription
    {
        private static readonly Dictionary<Type, IReadOnlyList<JsonFieldDescription>> cache = new Dictionary<Type, IReadOnlyList<JsonFieldDescription>>();
        private static readonly Dictionary<FieldInfo, JsonFieldDescription> fields = new Dictionary<FieldInfo, JsonFieldDescription>();
        public FieldInfo Field { get; }
        public string Label { get; }
        public OptionRelateParamAttribute Condition { get; }
        public OptionParamAttribute Options { get; }
        public SelectObjectPathAttribute ResourcePath { get; }
        public RangeAttribute Range { get; }
        public IReadOnlyList<FieldInfo> Constants { get; }
        public bool Hidden { get; }
        private JsonFieldDescription(FieldInfo field)
        {
            Field = field; Label = field.GetCustomAttribute<MenuNameAttribute>()?.showName;
            Condition = field.GetCustomAttribute<OptionRelateParamAttribute>();
            Options = field.GetCustomAttribute<OptionParamAttribute>(); ResourcePath = field.GetCustomAttribute<SelectObjectPathAttribute>();
            Range = field.GetCustomAttribute<RangeAttribute>(); Hidden = field.IsDefined(typeof(HideInInspector));
            Constants = Array.AsReadOnly(Options == null ? Array.Empty<FieldInfo>() : Options.classType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(int)).OrderBy(f => f.GetCustomAttribute<OptionSortAttribute>()?.sort ?? 99).ToArray());
        }
        public static JsonFieldDescription For(FieldInfo field)
        {
            if (!fields.TryGetValue(field, out var description)) fields[field] = description = new JsonFieldDescription(field);
            return description;
        }
        public static IReadOnlyList<JsonFieldDescription> For(Type type)
        {
            if (!cache.TryGetValue(type, out var result)) cache[type] = result = Array.AsReadOnly(type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(For).ToArray());
            return result;
        }
        public bool Visible(object owner)
        {
            if (Hidden) return false;
            var related = Condition == null ? null : owner.GetType().GetField(Condition.argsName);
            return related == null || Condition.argsValue.Any(value => Equals(value, related.GetValue(owner)));
        }
        public int OptionIndex(object value) => Constants.ToList().FindIndex(f => Equals(f.GetRawConstantValue(), value));
    }

}
