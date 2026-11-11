using System;

namespace NBC.ActionEditor
{
    public struct TypeMetaInfo
    {
        public Type type;
        public string name;
        public string category;
        public Type[] attachableTypes;
        public bool isUnique;
    }
}
