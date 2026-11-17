using System;

namespace ActionSequenceEditor
{
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

}
