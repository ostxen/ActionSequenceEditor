using NBC.ActionEditor.Presentation.Properties;
using System;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Domain;
using UnityEngine.UIElements;

namespace NBC.ActionEditor
{
    // 扩展只捕获显示值，不取得作者图、路径或默认宿主。
    public sealed class EditorExtensionContext
    {
        private readonly Func<NBC.ActionEditor.Presentation.Contracts.ExtensionDocumentState> capture;
        public NBC.ActionEditor.Presentation.Contracts.ExtensionDocumentState Capture() => capture();
        public EditorExtensionContext(Func<NBC.ActionEditor.Presentation.Contracts.ExtensionDocumentState> capture)
        { this.capture = capture ?? throw new ArgumentNullException(nameof(capture)); }
    }

    // 编辑器扩展绑定明确目标类型与子类策略，不进入Runtime业务声明。
    public abstract class EditorViewBindingAttribute : Attribute
    {
        public Type Target { get; }
        public bool Children { get; }
        protected EditorViewBindingAttribute(Type target, bool children) { Target = target; Children = children; }
    }
    // IMGUI属性绘制的显式绑定。
    public sealed class CustomInspectors : EditorViewBindingAttribute
    { public CustomInspectors(Type target, bool children = false) : base(target, children) { } }
    // IMGUI工具栏绘制的显式绑定。
    public sealed class CustomHeader : EditorViewBindingAttribute
    { public CustomHeader(Type target, bool children = false) : base(target, children) { } }
    // Toolkit工具栏构建的独立绑定。
    public sealed class ToolkitHeaderAttribute : EditorViewBindingAttribute
    { public ToolkitHeaderAttribute(Type target, bool children = false) : base(target, children) { } }
    // Toolkit属性控件构建的独立绑定。
    public sealed class ToolkitPropertiesAttribute : EditorViewBindingAttribute
    { public ToolkitPropertiesAttribute(Type target, bool children = false) : base(target, children) { } }
    // 原生Toolkit Header不承载IMGUI绘制回调。
    public abstract class ToolkitHeaderView
    { public abstract VisualElement CreateGUI(EditorExtensionContext context); }
    // 业务属性扩展只获得所属属性Presenter及显式目标。
    public abstract class ToolkitPropertyExtension
    { public abstract void Populate(VisualElement root, NBC.ActionEditor.Presentation.Properties.PropertyTargetState target, PropertyPresenter presenter, Action refresh); }
}
