using System;
using ActionSequenceEditor.Application.Ports;
using ActionSequenceEditor.Model;
using ActionSequenceEditor.Infrastructure.Serialization;

namespace ActionSequenceEditor.Infrastructure.Sequences
{
    // 类型目录检查先于业务构造，避免菜单或保存发现执行未知构造器。
    public sealed class SequenceFactory : ISequenceFactory
    {
        public ActionSequence Create(Type type)
        {
            if (type == null || !typeof(ActionSequence).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters
                || type.GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException("无效的资源类型。");
            SequenceFormats.Context.RequireType(type, "$.document");
            return (ActionSequence)Activator.CreateInstance(type);
        }
    }
}
