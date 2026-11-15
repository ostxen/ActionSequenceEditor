using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Infrastructure.Assets;
using NBC.ActionEditor.Views.IMGUI.Properties;
using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    // 解析业务扩展绑定并按原生技术创建视图实例。
    public static class EditorExtensionFactory
    {
        private static Type[] headers, inspectors;
        private static readonly Dictionary<Type, Type> headerBindings = new Dictionary<Type, Type>();
        private static readonly Dictionary<Type, Type> inspectorBindings = new Dictionary<Type, Type>();
        private static Type[] Discover(Type type) => ActionEditorTypeCatalog.DerivedFrom(type).Select(i => i.type).ToArray();

        public static HeaderBase GetHeader(Type asset)
        {
            if (asset == null) return null;
            headers ??= Discover(typeof(HeaderBase));
            if (!headerBindings.TryGetValue(asset, out var type))
                headerBindings[asset] = type = ExtensionResolver.ResolveExtension(headers, asset, false);
            return type == null ? null : (HeaderBase)Activator.CreateInstance(type);
        }
        public static InspectorsBase GetInspector(NBC.ActionEditor.Presentation.Properties.PropertyTargetState value, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter)
        {
            var type = GetInspectorType(value.Type);
            var inspector = type == null ? new InspectorsBase() : (InspectorsBase)Activator.CreateInstance(type);
            inspector.Bind(presenter); inspector.SetTarget(value); return inspector;
        }

        public static Type GetInspectorType(Type target)
        {
            inspectors ??= Discover(typeof(InspectorsBase));
            if (!inspectorBindings.TryGetValue(target, out var type))
                inspectorBindings[target] = type = ExtensionResolver.ResolveExtension(inspectors, target, true);
            return type;
        }

        public static ToolkitHeaderView GetToolkitHeader(Type document)
        {
            var type = document == null ? null : ExtensionResolver.ResolveExtension(Discover(typeof(ToolkitHeaderView)), document, typeof(ToolkitHeaderAttribute));
            return type == null ? null : (ToolkitHeaderView)Activator.CreateInstance(type);
        }
        public static ToolkitPropertyExtension GetToolkitProperties(Type target)
        {
            var type = ExtensionResolver.ResolveExtension(Discover(typeof(ToolkitPropertyExtension)), target, typeof(ToolkitPropertiesAttribute));
            return type == null ? null : (ToolkitPropertyExtension)Activator.CreateInstance(type);
        }

    }
}