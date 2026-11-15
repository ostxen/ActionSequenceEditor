using System;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Infrastructure.Serialization;

namespace NBC.ActionEditor.Infrastructure.Documents
{
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
}
