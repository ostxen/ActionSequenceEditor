using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 定义时间对象的遍历、状态及生命周期，不携带编辑器视图。
    public interface IDirectable : IData
    {
        IDirector Root { get; }
        IDirectable Parent { get; }
        IEnumerable<IDirectable> Children { get; }

        GameObject Actor { get; }
        string Name { get; }

        bool IsActive { get; set; }
        bool IsLocked { get; set; }

        float StartTime { get; }
        float EndTime { get; }

        float BlendIn { get; set; }
        float BlendOut { get; set; }
        bool CanCrossBlend { get; }

        void Validate(IDirector root, IDirectable parent);
        bool Initialize();
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }


    // 提供片段参数求值目标，不决定预览资源的创建和释放。
    public interface IClip : IDirectable
    {
        object AnimatedParametersTarget { get; }
    }
}