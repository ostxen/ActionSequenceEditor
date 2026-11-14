using System;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Application.Ports
{
    // 剪贴片段的类型与内容由存储适配器隔离，不暴露具体格式。
    public interface IFragmentCodec
    {
        string Capture(IDirectable item);
        IDirectable Restore(Type type, string snapshot);
    }

    // 创建目录由宿主控制，应用用例不发现或实例化未注册业务类型。
    public interface IDocumentFactory
    {
        ActionDocument Create(Type type);
    }

    // 属性默认值涉及宿主对象类型，由适配器决定可构造能力。
    public interface IPropertyValueFactory
    {
        object Create(Type type);
        object Copy(object value);
    }
}
