using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Serialization;
using UnityEditor.Compilation;
using Object = UnityEngine.Object;

namespace ActionSequenceEditor.Infrastructure.Serialization
{
    // 装配唯一格式目录与生产受信任类型上下文。
    public static class SequenceFormats
    {
        public static SequenceFormatRegistry Registry { get; } = new SequenceFormatRegistry(new ISequenceFormat[] { new SequenceJsonFormat() });
        private static SequenceFormatContext production;
        public static SequenceFormatContext Context => production ??= CreateContext();
        // 只发现已加载的生产类型；测试类型必须由隔离调用显式提供。
        public static SequenceFormatContext CreateContext(params Type[] explicitTestTypes)
        {
            var editorOnly = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Where(a => (a.flags & AssemblyFlags.EditorAssembly) != 0).Select(a => a.name), StringComparer.Ordinal);
            var trusted = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (assembly.IsDynamic || editorOnly.Contains(name) || name.EndsWith(".Tests", StringComparison.Ordinal)
                    || name.IndexOf(".Verification", StringComparison.Ordinal) >= 0 || name.EndsWith(".Editor", StringComparison.Ordinal)
                    || name.StartsWith("UnityEditor", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException error) { types = error.Types.Where(t => t != null).ToArray(); }
                trusted.AddRange(types.Where(t => t.IsSerializable && !t.IsAbstract && !t.ContainsGenericParameters
                    && t.Namespace != null && !t.Namespace.StartsWith("System", StringComparison.Ordinal)
                    && !t.Namespace.StartsWith("Unity", StringComparison.Ordinal) && !typeof(Object).IsAssignableFrom(t)));
            }
            return new SequenceFormatContext(trusted.Concat(explicitTestTypes), new AssetReferenceResolver());
        }
    }

}
