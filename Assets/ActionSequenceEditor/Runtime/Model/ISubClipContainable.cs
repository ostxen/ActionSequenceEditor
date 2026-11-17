namespace ActionSequenceEditor
{
    // 描述片段内部循环的偏移、速度和周期。
    public interface ISubClipContainable : IDirectable
    {
        float SubClipOffset { get; set; }
        
        float SubClipSpeed { get; }
        
        float SubClipLength { get; }
    }
}