using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor.Infrastructure.Assets
{
    // 按显式绑定与继承距离选择唯一视图扩展。
    public static class ExtensionResolver
    {
        public static Type ResolveExtension(IEnumerable<Type> candidates, Type target, bool inspector)
            => ResolveExtension(candidates, target, inspector ? typeof(CustomInspectors) : typeof(CustomHeader));

        // 最近目标优先；同目标仅允许唯一最派生适配，禁止按扫描顺序覆盖。
        public static Type ResolveExtension(IEnumerable<Type> candidates, Type target, Type binding)
        {
            for (var current = target; current != null; current = current.BaseType)
            {
                var matches = candidates.Where(t => !t.IsAbstract && !t.ContainsGenericParameters &&
                    t.GetCustomAttributes(binding, true).Cast<EditorViewBindingAttribute>()
                        .Any(a => a.Target == current && (current == target || a.Children)))
                    .Distinct().ToArray();
                // 同一绑定的派生适配可以明确替换基类，不依赖程序集枚举顺序。
                var mostDerived = matches.Where(t => !matches.Any(other => other != t && t.IsAssignableFrom(other))).ToArray();
                if (mostDerived.Length > 1) throw new InvalidOperationException("ActionEditor扩展冲突：" + current.AssemblyQualifiedName + " => "
                    + string.Join("; ", mostDerived.OrderBy(t => t.AssemblyQualifiedName).Select(t => t.AssemblyQualifiedName)));
                if (mostDerived.Length == 1) return mostDerived[0];
            }
            return null;
        }
    }
}
