using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor.Infrastructure.Assets
{
    // 编辑器产品目录仅发现可发布类型，缓存结果不向调用者暴露可写数组。
    public static class ActionEditorTypeCatalog
    {
        private static readonly Dictionary<Type, TypeMetaInfo[]> menus = new Dictionary<Type, TypeMetaInfo[]>();
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
        public static List<TypeMetaInfo> DerivedFrom(Type baseType)
        {
            if (!menus.TryGetValue(baseType, out var items))
            {
                items = ReflectionTools.GetImplementationsOf(baseType)
                    .Where(t => ProductTypes.Includes(t) && !t.ContainsGenericParameters && !t.IsDefined(typeof(ObsoleteAttribute), true))
                    .Select(t =>
                    {
                        var info = DirectableTypeInfo.For(t);
                        return new TypeMetaInfo { type = t, name = info.MenuName, category = info.Category,
                            attachableTypes = info.AttachableTypes?.ToArray(), isUnique = info.IsUnique };
                    }).OrderBy(i => i.name).ThenBy(i => i.category).ToArray();
                menus[baseType] = items;
            }
            // 各菜单可以调整自己的列表，不能改变共享目录缓存。
            return items.Select(i => new TypeMetaInfo { type = i.type, name = i.name, category = i.category,
                attachableTypes = i.attachableTypes == null ? null : (Type[])i.attachableTypes.Clone(), isUnique = i.isUnique }).ToList();
        }
    }
}
