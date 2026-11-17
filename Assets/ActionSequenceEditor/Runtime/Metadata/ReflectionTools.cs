using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ActionSequenceEditor
{
    // 从已加载程序集发现公开实现类型，并读取业务声明的类型特性。
    public static class ReflectionTools
    {
        private static Assembly[] loadedAssemblies;
        private static readonly Dictionary<Type, Type[]> implementations = new Dictionary<Type, Type[]>();

        public static Type[] GetImplementationsOf(Type type)
        {
            if (!implementations.TryGetValue(type, out var result))
            {
                loadedAssemblies ??= AppDomain.CurrentDomain.GetAssemblies();
                var found = new List<Type>();
                foreach (var assembly in loadedAssemblies)
                {
                    // 单个不可枚举程序集不阻断其他业务扩展的发现；不额外加载磁盘程序集。
                    try { found.AddRange(assembly.GetExportedTypes().Where(candidate => type.IsAssignableFrom(candidate) && !candidate.IsAbstract)); }
                    catch { continue; }
                }
                result = found.ToArray();
                implementations.Add(type, result);
            }
            return (Type[])result.Clone();
        }

        public static T RTGetAttribute<T>(this Type type, bool inherited) where T : Attribute =>
            (T)type.GetCustomAttributes(typeof(T), inherited).FirstOrDefault();
    }
}