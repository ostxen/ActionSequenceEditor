using NBC.ActionEditor.Model;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 缓存类型声明能力；锁定、启用和混合哨兵值仍由实例决定。
    public sealed class DirectableTypeInfo
    {
        private static readonly Dictionary<Type, DirectableTypeInfo> cache = new Dictionary<Type, DirectableTypeInfo>();
        public string Name { get; }
        public string MenuName { get; }
        public string Category { get; }
        public Color Color { get; }
        public IReadOnlyList<Type> AttachableTypes { get; }
        public bool IsUnique { get; }
        public bool IsSignal { get; }
        public bool CanScale { get; }
        public bool HasBlendIn { get; }
        public bool HasBlendOut { get; }
        private DirectableTypeInfo(Type type)
        {
            var name = type.GetCustomAttribute<NameAttribute>(true)?.name;
            Name = name ?? type.Name; MenuName = name ?? type.Name.SplitCamelCase();
            Category = type.GetCustomAttribute<CategoryAttribute>(true)?.category;
            Color = type.GetCustomAttribute<ColorAttribute>(true)?.Color ?? UnityEngine.Color.gray;
            var parents = type.GetCustomAttribute<AttachableAttribute>(true)?.Types;
            AttachableTypes = parents == null ? null : Array.AsReadOnly((Type[])parents.Clone());
            IsUnique = type.IsDefined(typeof(UniqueAttribute), true);
            IsSignal = typeof(PointClip).IsAssignableFrom(type);
            CanScale = WritableOverride(type, "Length");
            HasBlendIn = WritableOverride(type, "BlendIn"); HasBlendOut = WritableOverride(type, "BlendOut");
        }
        private static bool WritableOverride(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.CanWrite && property.DeclaringType != typeof(ActionClip);
        }
        public static DirectableTypeInfo For(Type type)
        {
            if (!cache.TryGetValue(type, out var info)) cache[type] = info = new DirectableTypeInfo(type);
            return info;
        }
    }
}
