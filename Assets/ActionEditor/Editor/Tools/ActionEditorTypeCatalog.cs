using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    public static class ActionEditorTypeCatalog
    {
        private static readonly Dictionary<Type, EditorTools.TypeMetaInfo[]> menus = new Dictionary<Type, EditorTools.TypeMetaInfo[]>();
        private static readonly Dictionary<Type, Type> previewBindings = new Dictionary<Type, Type>();
        public static Type ResolvePreview(IEnumerable<Type> candidates, Type target)
        {
            var matches = candidates.Where(type => !type.IsAbstract && !type.ContainsGenericParameters && type.GetCustomAttributes(typeof(CustomPreviewAttribute), true)
                .Cast<CustomPreviewAttribute>().Any(attribute => attribute.PreviewType == target)).Distinct().ToArray();
            var best = matches.Where(type => !matches.Any(other => type != other && type.IsAssignableFrom(other))).ToArray();
            if (best.Length > 1) throw new InvalidOperationException("ActionEditor预览扩展冲突：" + target.AssemblyQualifiedName + " => "
                + string.Join("; ", best.Select(type => type.AssemblyQualifiedName).OrderBy(name => name)));
            return best.SingleOrDefault();
        }
        public static PreviewBase CreatePreview(Type target)
        {
            if (!previewBindings.TryGetValue(target, out var type))
                previewBindings[target] = type = ResolvePreview(DerivedFrom(typeof(PreviewBase)).Select(info => info.type), target);
            return type == null ? null : (PreviewBase)Activator.CreateInstance(type);
        }
        public static List<EditorTools.TypeMetaInfo> DerivedFrom(Type baseType)
        {
            if (!menus.TryGetValue(baseType, out var items))
            {
                items = ReflectionTools.GetImplementationsOf(baseType)
                    .Where(t => !t.ContainsGenericParameters && !t.IsDefined(typeof(ObsoleteAttribute), true))
                    .Select(t =>
                    {
                        var info = DirectableTypeInfo.For(t);
                        return new EditorTools.TypeMetaInfo { type = t, name = info.MenuName, category = info.Category,
                            attachableTypes = info.AttachableTypes?.ToArray(), isUnique = info.IsUnique };
                    }).OrderBy(i => i.name).ThenBy(i => i.category).ToArray();
                menus[baseType] = items;
            }
            // Old callers own mutable lists and arrays; never expose the cached copies.
            return items.Select(i => new EditorTools.TypeMetaInfo { type = i.type, name = i.name, category = i.category,
                attachableTypes = i.attachableTypes == null ? null : (Type[])i.attachableTypes.Clone(), isUnique = i.isUnique }).ToList();
        }
    }
}
