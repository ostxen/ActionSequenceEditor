using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 属性控件由宿主显式取得共享编辑用例，Presenter本身不查找全局状态。
    public static class PropertyComposition
    {
        public static PropertyPresenter Create(ActionEditorContext context) =>
            new PropertyPresenter(context.Properties, context.Structure, context.Clips, () => Prefs.Session.Current, context.Selection);
    }
}
