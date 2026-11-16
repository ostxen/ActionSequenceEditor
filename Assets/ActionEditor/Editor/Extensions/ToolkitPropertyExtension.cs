using System;
using NBC.ActionEditor.Presentation.Presenters;
using UnityEngine.UIElements;

namespace NBC.ActionEditor
{
    // 业务属性扩展只获得所属属性Presenter及显式目标。
    public abstract class ToolkitPropertyExtension
    { public abstract void Populate(VisualElement root, NBC.ActionEditor.Presentation.Properties.PropertyTargetState target, PropertyPresenter presenter, Action refresh); }
}
