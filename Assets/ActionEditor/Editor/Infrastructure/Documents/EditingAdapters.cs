using System;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Serialization;

namespace NBC.ActionEditor.Infrastructure.Documents
{
    // 片段独立编码，不携带所属文档或窗口引用。
    public sealed class FragmentCodec : IFragmentCodec
    {
        public string Capture(IDirectable item) => DocumentSnapshots.CaptureFragment(item);
        public IDirectable Restore(Type type, string snapshot) => DocumentSnapshots.RestoreFragment(type, snapshot);
    }

    // 类型目录检查先于业务构造，避免菜单或保存发现执行未知构造器。
    public sealed class DocumentFactory : IDocumentFactory
    {
        public ActionDocument Create(Type type)
        {
            if (type == null || !typeof(ActionDocument).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters
                || type.GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException("无效的资源类型。");
            DocumentFormats.Context.RequireType(type, "$.document");
            return (ActionDocument)Activator.CreateInstance(type);
        }
    }

    // Unity 对象默认空引用，普通可构造值按其声明类型创建。
    public sealed class PropertyValueFactory : IPropertyValueFactory
    {
        private readonly DocumentFormatContext context;
        public PropertyValueFactory(DocumentFormatContext context = null) { this.context = context; }
        // 可变业务值沿用受控字段转换，Unity资源引用保留身份。
        public object Copy(object value)
        {
            if (value == null || value is string || value is UnityEngine.Object) return value;
            var type = value.GetType();
            if (type.IsValueType && !ContainsMutableReference(type)) return value;
            var format = context ?? DocumentFormats.Context;
            return DocumentValueConverter.Restore(DocumentValueConverter.Capture(value, type, format), type, format);
        }
        // struct按值传递不等于深拷贝；其内部列表、数组和业务对象仍需隔离。
        private static bool ContainsMutableReference(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if (!type.IsValueType) return true;
            return System.Array.Exists(DocumentValueConverter.Fields(type), field => ContainsMutableReference(field.FieldType));
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
