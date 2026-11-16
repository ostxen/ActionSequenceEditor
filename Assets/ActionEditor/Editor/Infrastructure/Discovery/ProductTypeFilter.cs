using System;
using System.Linq;
using System.Reflection;

namespace NBC.ActionEditor.Infrastructure.Discovery
{
    // 产品发现排除测试和工程验证程序集，不构造候选类型。
    public static class ProductTypeFilter
    {
        public static bool Includes(Type type)
        {
            if (type == null || type.Assembly.IsDynamic) return false;
            var name = type.Assembly.GetName().Name;
            if (name.Split('.').Any(part => part == "Tests" || part == "Test" || part == "Verification")) return false;
            return !type.Assembly.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "UnityEngine.TestTools.TestRunner.TestAssemblyAttribute");
        }
    }
}
