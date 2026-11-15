using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Application.Editing;

namespace NBC.ActionEditor.Composition
{
    // 属性控件由宿主显式取得共享编辑用例，Presenter本身不查找全局状态。
    public static class PropertyComposition
    {
        public static PropertyPresenter Create(PropertyEditing properties, NodeEditing nodes, ClipEditing clips, SelectionState selection, SettingsSession settings)
        {
            if (settings == null) throw new System.ArgumentNullException(nameof(settings));
            return new PropertyPresenter(properties, nodes, clips, () => settings.Current, selection);
        }
    }
}
