using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    public static class EditorCustomFactory
    {
        private static Type[] headers, inspectors;
        private static readonly Dictionary<Type, Type> headerBindings = new Dictionary<Type, Type>();
        private static readonly Dictionary<Type, Type> inspectorBindings = new Dictionary<Type, Type>();
        public static void InitHeaderDic() { if (headers == null) headers = Discover(typeof(HeaderBase)); }
        public static void InitInspectorDic() { if (inspectors == null) inspectors = Discover(typeof(InspectorsBase)); }
        private static Type[] Discover(Type type) => ActionEditorTypeCatalog.DerivedFrom(type).Select(i => i.type).ToArray();

        public static HeaderBase GetHeader(ActionDocument asset)
        {
            if (asset == null) return null;
            InitHeaderDic();
            if (!headerBindings.TryGetValue(asset.GetType(), out var type))
                headerBindings[asset.GetType()] = type = ExtensionResolver.ResolveExtension(headers, asset.GetType(), false);
            return type == null ? null : (HeaderBase)Activator.CreateInstance(type);
        }
        public static InspectorsBase GetInspector(IData value, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter)
        {
            var type = GetInspectorType(value.GetType());
            var inspector = type == null ? new InspectorsBase() : (InspectorsBase)Activator.CreateInstance(type);
            inspector.Bind(presenter); inspector.SetTarget(value); return inspector;
        }

        public static Type GetInspectorType(Type target)
        {
            InitInspectorDic();
            if (!inspectorBindings.TryGetValue(target, out var type))
                inspectorBindings[target] = type = ExtensionResolver.ResolveExtension(inspectors, target, true);
            return type;
        }

        public static ToolkitHeaderView GetToolkitHeader(ActionDocument document)
        {
            var type = document == null ? null : ExtensionResolver.ResolveExtension(Discover(typeof(ToolkitHeaderView)), document.GetType(), typeof(ToolkitHeaderAttribute));
            return type == null ? null : (ToolkitHeaderView)Activator.CreateInstance(type);
        }
        public static ToolkitPropertyExtension GetToolkitProperties(Type target)
        {
            var type = ExtensionResolver.ResolveExtension(Discover(typeof(ToolkitPropertyExtension)), target, typeof(ToolkitPropertiesAttribute));
            return type == null ? null : (ToolkitPropertyExtension)Activator.CreateInstance(type);
        }

    }
}