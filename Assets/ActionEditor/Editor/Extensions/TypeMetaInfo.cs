using System;

namespace NBC.ActionEditor
{
    // 菜单消费的类型说明副本，不持有业务实例或共享可写类型目录。
    public struct TypeMetaInfo
    {
        public Type type;
        public string name;
        public string category;
        public Type[] attachableTypes;
        public bool isUnique;
    }
}
