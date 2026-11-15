using System;
using System.Collections.Generic;
using NBC.ActionEditor.Application.Editing;

namespace NBC.ActionEditor.Presentation.Properties
{
    // 字段身份不携带路径或作者引用，只能由签发它的Presenter解析。
    public readonly struct PropertyFieldId : IEquatable<PropertyFieldId>
    {
        internal object Issuer { get; }
        internal long Generation { get; }
        internal int Index { get; }
        public object ViewKey { get; }
        internal PropertyFieldId(object issuer, long generation, int index, object viewKey)
        { Issuer = issuer; Generation = generation; Index = index; ViewKey = viewKey; }
        public bool Equals(PropertyFieldId other) => ReferenceEquals(Issuer, other.Issuer)
            && Generation == other.Generation && Index == other.Index;
        public override bool Equals(object other) => other is PropertyFieldId id && Equals(id);
        public override int GetHashCode() => (Issuer?.GetHashCode() ?? 0) ^ Generation.GetHashCode() ^ Index;
    }

    // 新属性接口只交付值和声明，不暴露FieldInfo、PropertyPath或作者对象。
    public sealed class PropertyInputState
    {
        public PropertyFieldId Id { get; }
        public string Name { get; }
        public string Label { get; }
        public Type Type { get; }
        public object Value { get; }
        public bool IsNull { get; }
        public bool Mixed { get; }
        public bool HasRange { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public Type ResourceType { get; }
        public bool HasOptions { get; }
        public IReadOnlyList<string> OptionLabels { get; }
        public int OptionIndex { get; }
        public IReadOnlyList<PropertyInputState> Children { get; }
        internal PropertyInputState(PropertyFieldId id, string name, string label, Type type, object value,
            bool isNull, bool mixed, PropertyDescription description, string[] options, int optionIndex, PropertyInputState[] children)
        {
            Id = id; Name = name; Label = label; Type = type; Value = value; IsNull = isNull; Mixed = mixed;
            HasRange = description?.HasRange ?? false; Minimum = description?.Minimum ?? 0;
            Maximum = description?.Maximum ?? 0; ResourceType = description?.ResourceType;
            HasOptions = description?.OptionType != null;
            OptionLabels = Array.AsReadOnly((string[])options.Clone()); OptionIndex = optionIndex;
            Children = Array.AsReadOnly(children);
        }
    }

    // 属性目标同样只携带显示信息，名称和时间意图使用不透明身份回传。
    public sealed class PropertyTargetState
    {
        public PropertyFieldId Id { get; }
        public Type Type { get; }
        public string Title { get; }
        public string Description { get; }
        public string Name { get; }
        public bool Locked { get; }
        public bool CanRename { get; }
        public ClipPropertyState Clip { get; }
        public IReadOnlyList<PropertyInputState> Fields { get; }
        internal PropertyTargetState(PropertyFieldId id, Type type, string title, string description,
            string name, bool locked, bool canRename, ClipPropertyState clip, IReadOnlyList<PropertyInputState> fields)
        {
            Id = id; Type = type; Title = title; Description = description; Name = name;
            Locked = locked; CanRename = canRename; Clip = clip; Fields = fields;
        }
    }

    // 诊断只交付本次文档代次和不透明位置，不暴露可写片段。
    public readonly struct PropertyIssueState
    {
        public int Key { get; }
        public long Version { get; }
        public string Message { get; }
        internal PropertyIssueState(int key, long version, string message)
        { Key = key; Version = version; Message = message; }
    }

}
