using ActionSequenceEditor.Application.Settings;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;

namespace ActionSequenceEditor.Composition
{
    // 两种原生偏好视图共享会话，但各自持有Presenter草稿与订阅。
    public static class SettingsPresenterComposition
    {
        public static SettingsPresenter Create(ISettingsView view, SettingsSession session) => new SettingsPresenter(session, view);
    }
}
