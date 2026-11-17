namespace ActionSequenceEditor.Presentation.Properties
{
    public enum ClipPropertyWarning { None, Outside, StartBeforeParent, EndAfterParent }

    // 原生时间与混合控件仅消费值快照，不遍历片段邻居或父对象。
    public sealed class ClipPropertyState
    {
        public bool Valid { get; }
        public bool Scalable { get; }
        public bool ShowRange { get; }
        public float Start { get; }
        public float End { get; }
        public float Length => End - Start;
        public float Minimum { get; }
        public float Maximum { get; }
        public float PointMaximum { get; }
        public bool BlendInEnabled { get; }
        public bool BlendOutEnabled { get; }
        public float BlendIn { get; }
        public float BlendOut { get; }
        public float BlendInMaximum { get; }
        public float BlendOutMaximum { get; }
        public ClipPropertyWarning Warning { get; }

        public ClipPropertyState(bool valid, bool scalable, bool showRange, float start, float end,
            float minimum, float maximum, float pointMaximum, bool blendInEnabled, bool blendOutEnabled,
            float blendIn, float blendOut, float blendInMaximum, float blendOutMaximum, ClipPropertyWarning warning)
        {
            Valid = valid; Scalable = scalable; ShowRange = showRange; Start = start; End = end;
            Minimum = minimum; Maximum = maximum; PointMaximum = pointMaximum;
            BlendInEnabled = blendInEnabled; BlendOutEnabled = blendOutEnabled;
            BlendIn = blendIn; BlendOut = blendOut; BlendInMaximum = blendInMaximum; BlendOutMaximum = blendOutMaximum; Warning = warning;
        }
    }
}
