using System;
using System.Collections.Generic;
using NBC.ActionEditor.Application.Editing;

namespace NBC.ActionEditor.Presentation.Contracts
{
    // 复杂字段只向视图交付子字段快照，不保留作者对象引用。
    public sealed class PropertyObjectState
    {
        public IReadOnlyList<PropertyFieldState> Fields { get; }
        internal PropertyObjectState(PropertyFieldState[] fields) { Fields = Array.AsReadOnly(fields); }
    }

    // 字段描述与当前显示值；曲线独立复制，作者写入必须携带所属路径。
    public sealed class PropertyFieldState
    {
        public PropertyDescription Description { get; }
        public PropertyPath Path { get; }
        public object Value { get; }
        public Type Type => Description.Field.FieldType;
        public string Label => Description.Label ?? Description.Field.Name;
        private readonly string[] optionLabels;
        public string[] OptionLabels => (string[])optionLabels.Clone();
        public int OptionIndex { get; }
        internal PropertyFieldState(PropertyDescription description, PropertyPath path, object value)
        {
            Description = description; Path = path; Value = value;
            OptionIndex = description.OptionIndex(Value);
            optionLabels = new string[description.Constants.Count];
            for (var i = 0; i < optionLabels.Length; i++)
            {
                var field = description.Constants[i];
                var name = (MenuNameAttribute)Attribute.GetCustomAttribute(field, typeof(MenuNameAttribute));
                optionLabels[i] = name?.showName ?? field.Name;
            }
        }
        public object OptionValue(int index) => Description.Constants[index].GetRawConstantValue();

        // 曲线控件允许原位修改，不能向它交出作者图中的同一实例。
        public static object CopyValue(object value) => value is UnityEngine.AnimationCurve curve
            ? new UnityEngine.AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode }
            : value;
    }
}
