using ActionSequenceEditor.Application.Settings;

namespace ActionSequenceEditor.Presentation.Contracts
{
    // 只接收偏好快照及当前操作错误，不暴露可写会话。
    public interface ISettingsView
    {
        void Render(EditorSettings state);
        void ShowError(string message);
    }
}
