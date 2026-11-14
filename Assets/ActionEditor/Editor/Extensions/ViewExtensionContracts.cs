using System;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Domain;
using UnityEngine.UIElements;

namespace NBC.ActionEditor
{
    // 扩展借用所属窗口的文档与播放头读取，不寻找另一全局会话。
    public sealed class EditorExtensionContext
    {
        private readonly Func<ActionDocument> document;
        private readonly Func<float> time;
        public ActionDocument Document => document();
        public float Time => time();
        public EditorExtensionContext(Func<ActionDocument> document, Func<float> time)
        { this.document = document ?? throw new ArgumentNullException(nameof(document)); this.time = time ?? throw new ArgumentNullException(nameof(time)); }
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
    { public abstract void Populate(VisualElement root, object target, PropertyPresenter presenter, Action refresh); }
}
