using System;
using ActionSequenceEditor.Application.Ports;
using ActionSequenceEditor.Serialization;

namespace ActionSequenceEditor.Infrastructure.Serialization
{
    // Unity 对象默认空引用，普通可构造值按其声明类型创建。
    public sealed class PropertyValueFactory : IPropertyValueFactory
    {
        private readonly SequenceFormatContext context;
        public PropertyValueFactory(SequenceFormatContext context = null) { this.context = context; }
        // 可变业务值沿用受控字段转换，Unity资源引用保留身份。
        public object Copy(object value)
        {
            if (value == null || value is string || value is UnityEngine.Object) return value;
            var type = value.GetType();
            if (type.IsValueType && !ContainsMutableReference(type)) return value;
            var format = context ?? SequenceFormats.Context;
            return JsonValueConverter.Restore(JsonValueConverter.Capture(value, type, format), type, format);
        }
        // struct按值传递不等于深拷贝；其内部列表、数组和业务对象仍需隔离。
        private static bool ContainsMutableReference(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if (!type.IsValueType) return true;
            return System.Array.Exists(JsonValueConverter.Fields(type), field => ContainsMutableReference(field.FieldType));
        }
        public object Create(Type type)
        {
            if (type == typeof(string)) return "";
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return null;
            if (type.IsAbstract || type.IsInterface || !type.IsValueType && type.GetConstructor(Type.EmptyTypes) == null) return null;
            return Activator.CreateInstance(type);
        }
    }
}
