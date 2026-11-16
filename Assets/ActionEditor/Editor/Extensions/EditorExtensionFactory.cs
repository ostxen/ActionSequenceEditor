using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Infrastructure.Discovery;
using NBC.ActionEditor.Views.IMGUI.Properties;
using NBC.ActionEditor.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    // 解析业务扩展绑定并按原生技术创建视图实例。
    public static class EditorExtensionFactory
    {
        private static Type[] headerTypes, inspectorTypes;
        private static readonly Dictionary<Type, Type> headerBindings = new Dictionary<Type, Type>();
        private static readonly Dictionary<Type, Type> inspectorBindings = new Dictionary<Type, Type>();
        private static Type[] Discover(Type type) => ActionEditorTypeCatalog.DerivedFrom(type).Select(i => i.type).ToArray();

        public static SequenceHeaderExtension CreateHeader(Type sequenceType)
        {
            if (sequenceType == null) return null;
            headerTypes ??= Discover(typeof(SequenceHeaderExtension));
            if (!headerBindings.TryGetValue(sequenceType, out var type))
                headerBindings[sequenceType] = type = ExtensionResolver.ResolveExtension(headerTypes, sequenceType, false);
            return type == null ? null : (SequenceHeaderExtension)Activator.CreateInstance(type);
        }
        public static PropertyInspectorBase CreateInspector(NBC.ActionEditor.Presentation.Properties.PropertyTargetState targetState, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter)
        {
            var type = ResolveInspectorType(targetState.Type);
            var inspector = type == null ? new PropertyInspectorBase() : (PropertyInspectorBase)Activator.CreateInstance(type);
            inspector.Bind(presenter); inspector.SetTarget(targetState); return inspector;
        }

        public static Type ResolveInspectorType(Type targetType)
        {
            inspectorTypes ??= Discover(typeof(PropertyInspectorBase));
            if (!inspectorBindings.TryGetValue(targetType, out var type))
                inspectorBindings[targetType] = type = ExtensionResolver.ResolveExtension(inspectorTypes, targetType, true);
            return type;
        }

        public static ToolkitHeaderView CreateToolkitHeader(Type sequenceType)
        {
            var type = sequenceType == null ? null : ExtensionResolver.ResolveExtension(Discover(typeof(ToolkitHeaderView)), sequenceType, typeof(ToolkitHeaderAttribute));
            return type == null ? null : (ToolkitHeaderView)Activator.CreateInstance(type);
        }
        public static ToolkitPropertyExtension CreateToolkitPropertyExtension(Type targetType)
        {
            var type = ExtensionResolver.ResolveExtension(Discover(typeof(ToolkitPropertyExtension)), targetType, typeof(ToolkitPropertiesAttribute));
            return type == null ? null : (ToolkitPropertyExtension)Activator.CreateInstance(type);
        }

    }
}