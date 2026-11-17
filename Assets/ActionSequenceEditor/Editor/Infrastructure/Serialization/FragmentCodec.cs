using System;
using ActionSequenceEditor.Application.Ports;

namespace ActionSequenceEditor.Infrastructure.Serialization
{
    // 片段独立编码，不携带所属文档或窗口引用。
    public sealed class FragmentCodec : IFragmentCodec
    {
        public string Capture(IDirectable item) => SequenceCopy.CaptureFragment(item);
        public IDirectable Restore(Type type, string snapshot) => SequenceCopy.RestoreFragment(type, snapshot);
    }

}
