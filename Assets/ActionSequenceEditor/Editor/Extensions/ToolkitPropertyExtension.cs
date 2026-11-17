using System;
using ActionSequenceEditor.Presentation.Presenters;
using UnityEngine.UIElements;

namespace ActionSequenceEditor
{
    // 业务属性扩展只获得所属属性Presenter及显式目标。
    public abstract class ToolkitPropertyExtension
    { public abstract void Populate(VisualElement root, ActionSequenceEditor.Presentation.Properties.PropertyTargetState target, PropertyPresenter presenter, Action refresh); }
}
