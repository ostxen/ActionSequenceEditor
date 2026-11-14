using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 状态窗口只借用共享上下文的只读采样入口，不注册第二个播放驱动。
    public static class FeatureSampleComposition
    {
        public static FeatureSamplePresenter Create(EditorExtensionContext context)
        {
            return new FeatureSamplePresenter(() => context.Document, () => context.Time);
        }
    }
}
