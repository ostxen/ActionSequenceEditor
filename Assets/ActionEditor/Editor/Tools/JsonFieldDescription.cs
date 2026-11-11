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

    // Paths resolve each segment again at write time and propagate boxed value types back to their owner.
    public sealed class JsonPropertyPath
    {
        private readonly Func<object> root;
        private readonly object[] segments;
        public JsonPropertyPath(object value) : this(() => value, Array.Empty<object>()) { }
        private JsonPropertyPath(Func<object> root, object[] segments) { this.root = root; this.segments = segments; }
        public JsonPropertyPath Field(FieldInfo field) => new JsonPropertyPath(root, segments.Concat(new object[] { field }).ToArray());
        public JsonPropertyPath Element(int index) => new JsonPropertyPath(root, segments.Concat(new object[] { index }).ToArray());
        private static object Get(object owner, object segment)
        {
            if (owner == null) throw new InvalidOperationException("属性路径的父对象已为空。");
            return segment is FieldInfo field ? field.GetValue(owner) : ((IList)owner)[(int)segment];
        }
        private static void Put(object owner, object segment, object value)
        {
            if (segment is FieldInfo field) field.SetValue(owner, value); else ((IList)owner)[(int)segment] = value;
        }
        public object Read()
        {
            var value = root(); foreach (var segment in segments) value = Get(value, segment); return value;
        }
        public bool Mixed(IEnumerable<JsonPropertyPath> others)
        {
            var value = Read(); return others.Any(path => !Equals(value, path.Read()));
        }
        public void ValidateOwner()
        {
            var current = root();
            if (current is IData data && !ReferenceEquals(current, App.AssetData) && (App.AssetData == null || !App.AssetData.directables.Contains(data as IDirectable)))
                throw new InvalidOperationException("属性所属对象已不在当前文档中。");
            if (current is IDirectable directable && directable.IsLocked) throw new InvalidOperationException("属性所属对象已锁定。");
        }
        public void Set(object value)
        {
            if (segments.Length == 0) throw new InvalidOperationException("不能替换属性根对象。");
            ValidateOwner();
            var owners = new object[segments.Length]; var current = root();
            for (var i = 0; i < segments.Length; i++) { owners[i] = current; current = Get(current, segments[i]); }
            Put(owners[segments.Length - 1], segments[segments.Length - 1], value);
            for (var i = segments.Length - 2; i >= 0; i--)
                if (owners[i + 1].GetType().IsValueType) Put(owners[i], segments[i], owners[i + 1]);
        }
        public void Write(object value) => Storage.JsonPropertyEditing.Change(() => Set(value));
        public void Transform(Func<object, object> change) => Storage.JsonPropertyEditing.Change(() =>
        {
            ValidateOwner(); Set(change(Read()));
        });
    }
}
