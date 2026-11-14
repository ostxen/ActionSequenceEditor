using NBC.ActionEditor.Application.Editing;

namespace NBC.ActionEditor.Presentation.Contracts
{
    // 一次延迟字段输入的身份与基线；草稿不会持有作者事务。
    public sealed class PropertyDraft
    {
        internal PropertyPath Path { get; }
        internal long Version { get; }
        public object Original { get; }
        internal object[] Originals { get; }
        public object Value { get; internal set; }
        public bool Active { get; internal set; } = true;
        internal PropertyDraft(PropertyPath path, long version)
        { Path = path; Version = version; Originals = path.ReadValues(); Original = Value = Originals[0]; }
    }
}
