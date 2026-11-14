using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Application.Settings;

namespace NBC.ActionEditor.Composition
{
    // 属性控件由宿主显式取得共享编辑用例，Presenter本身不查找全局状态。
    public static class PropertyComposition
    {
        public static PropertyPresenter Create(ActionEditorContext context, SettingsSession settings)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (settings == null) throw new System.ArgumentNullException(nameof(settings));
            return new PropertyPresenter(context.Properties, context.Structure, context.Clips, () => settings.Current, context.Selection);
        }
    }
}
