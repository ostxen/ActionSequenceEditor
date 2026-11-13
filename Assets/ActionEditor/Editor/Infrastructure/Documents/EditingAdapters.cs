using System;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Storage;

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
        public object Create(Type type)
        {
            if (type == typeof(string)) return "";
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return null;
            if (type.IsAbstract || type.IsInterface || !type.IsValueType && type.GetConstructor(Type.EmptyTypes) == null) return null;
            return Activator.CreateInstance(type);
        }
    }
}
