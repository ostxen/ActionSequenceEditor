using System;
using System.Linq;
using System.Reflection;

namespace ActionSequenceEditor.Infrastructure.Discovery
{
    // 产品发现排除测试和工程验证类型，不因合并程序集而误收内部辅助定义。
    public static class ProductTypeFilter
    {
        public static bool Includes(Type type)
        {
            if (type == null || type.Assembly.IsDynamic) return false;
            for (var current = type; current != null; current = current.DeclaringType)
                if ((current.Namespace ?? string.Empty).Split('.').Any(part => part == "Tests" || part == "Test" || part == "Verification")) return false;
            var name = type.Assembly.GetName().Name;
            if (name.Split('.').Any(part => part == "Tests" || part == "Test" || part == "Verification")) return false;
            return !type.Assembly.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "UnityEngine.TestTools.TestRunner.TestAssemblyAttribute");
        }
    }
}
