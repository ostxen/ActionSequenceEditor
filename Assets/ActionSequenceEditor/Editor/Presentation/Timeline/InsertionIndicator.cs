namespace ActionSequenceEditor.Presentation.Timeline
{
    // 视图只画主体全宽插入线，无提示文本或作者对象。
    public readonly struct InsertionIndicator
    {
        public readonly bool Visible, Valid;
        public readonly float Y, Width;
        public float Red => Valid ? .15f : .9f;
        public float Green => Valid ? .55f : .2f;
        public float Blue => Valid ? 1f : .2f;
        public InsertionIndicator(bool visible, bool valid, float y, float width)
        { Visible = visible; Valid = valid; Y = y; Width = width; }
    }
}
