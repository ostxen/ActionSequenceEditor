using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ActionSequenceEditor
{
    // 缓存业务字段声明，不发现或创建任何编辑器适配器。
    public sealed class PropertyFieldMetadata
    {
        private static readonly Dictionary<Type, IReadOnlyList<PropertyFieldMetadata>> cache = new Dictionary<Type, IReadOnlyList<PropertyFieldMetadata>>();
        private static readonly Dictionary<FieldInfo, PropertyFieldMetadata> fields = new Dictionary<FieldInfo, PropertyFieldMetadata>();
        public FieldInfo Field { get; }
        public string Label { get; }
        private readonly string relatedField;
        private readonly object[] relatedValues;
        public Type OptionType { get; }
        public Type ResourceType { get; }
        public bool HasRange { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public IReadOnlyList<FieldInfo> Constants { get; }
        public bool Hidden { get; }
        private PropertyFieldMetadata(FieldInfo field)
        {
            Field = field; Label = field.GetCustomAttribute<MenuNameAttribute>()?.showName;
            // 只复制声明值，不向视图暴露可修改的Attribute实例。
            var condition = field.GetCustomAttribute<OptionRelateParamAttribute>();
            relatedField = condition?.argsName;
            relatedValues = condition == null ? Array.Empty<object>() : (object[])condition.argsValue.Clone();
            OptionType = field.GetCustomAttribute<OptionParamAttribute>()?.classType;
            ResourceType = field.GetCustomAttribute<SelectObjectPathAttribute>()?.type;
            var range = field.GetCustomAttribute<RangeAttribute>();
            HasRange = range != null; Minimum = range?.min ?? 0; Maximum = range?.max ?? 0;
            Hidden = field.IsDefined(typeof(HideInInspector));
            Constants = Array.AsReadOnly(OptionType == null ? Array.Empty<FieldInfo>() : OptionType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(int)).OrderBy(f => f.GetCustomAttribute<OptionSortAttribute>()?.sort ?? 99).ToArray());
        }
        public static PropertyFieldMetadata For(FieldInfo field)
        {
            if (!fields.TryGetValue(field, out var description)) fields[field] = description = new PropertyFieldMetadata(field);
            return description;
        }
        public static IReadOnlyList<PropertyFieldMetadata> For(Type type)
        {
            if (!cache.TryGetValue(type, out var result)) cache[type] = result = Array.AsReadOnly(type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(For).ToArray());
            return result;
        }
        public bool Visible(object owner)
        {
            if (Hidden) return false;
            var related = relatedField == null ? null : owner.GetType().GetField(relatedField);
            return related == null || relatedValues.Any(value => Equals(value, related.GetValue(owner)));
        }
        public int OptionIndex(object value) => Constants.ToList().FindIndex(f => Equals(f.GetRawConstantValue(), value));
    }

}
