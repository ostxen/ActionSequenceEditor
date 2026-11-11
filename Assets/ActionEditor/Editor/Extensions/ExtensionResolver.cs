using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor
{
    public static class ExtensionResolver
    {
        public static Type ResolveExtension(IEnumerable<Type> candidates, Type target, bool inspector)
        {
            for (var current = target; current != null; current = current.BaseType)
            {
                var matches = candidates.Where(t => !t.IsAbstract && !t.ContainsGenericParameters &&
                    (inspector ? t.GetCustomAttributes(typeof(CustomInspectors), true).Cast<CustomInspectors>()
                        .Any(a => a.InspectedType == current && (current == target || a._editorForChildClasses))
                    : t.GetCustomAttributes(typeof(CustomHeader), true).Cast<CustomHeader>()
                        .Any(a => a.InspectedType == current && (current == target || a.EditorForChildClasses))))
                    .Distinct().ToArray();
                // Preserve the established more-derived adapter override, never assembly scan order.
                var mostDerived = matches.Where(t => !matches.Any(other => other != t && t.IsAssignableFrom(other))).ToArray();
                if (mostDerived.Length > 1) throw new InvalidOperationException("ActionEditor扩展冲突：" + current.AssemblyQualifiedName + " => "
                    + string.Join("; ", mostDerived.OrderBy(t => t.AssemblyQualifiedName).Select(t => t.AssemblyQualifiedName)));
                if (mostDerived.Length == 1) return mostDerived[0];
            }
            return null;
        }
    }
}
