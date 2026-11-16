using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 两种原生偏好视图共享会话，但各自持有Presenter草稿与订阅。
    public static class SettingsPresenterComposition
    {
        public static SettingsPresenter Create(ISettingsView view, SettingsSession session) => new SettingsPresenter(session, view);
    }
}
