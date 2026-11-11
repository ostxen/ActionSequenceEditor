using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    public static class ActionEditorTypeCatalog
    {
        private static readonly Dictionary<Type, EditorTools.TypeMetaInfo[]> menus = new Dictionary<Type, EditorTools.TypeMetaInfo[]>();
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
