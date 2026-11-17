using ActionSequenceEditor.Model;

namespace ActionSequenceEditor.Serialization
{
    // 定义格式转换能力，不承担文件提交或编辑会话职责。
    public interface ISequenceFormat
    {
        string Id { get; }
        string DisplayName { get; }
        string Extension { get; }
        bool CanRead { get; }
        bool CanWrite { get; }
        bool CanExport { get; }
        byte[] Serialize(ActionSequence sequence, SequenceFormatOptions options, SequenceFormatContext context);
        ActionSequence Deserialize(byte[] bytes, SequenceFormatContext context);
    }

    // 传递输出选项，不改变文档内容或格式能力。
    public readonly struct SequenceFormatOptions
    {
        public bool Compact { get; }
        public SequenceFormatOptions(bool compact) { Compact = compact; }
    }

}
