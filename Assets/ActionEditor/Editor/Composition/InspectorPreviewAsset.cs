using UnityEngine;

namespace NBC.ActionEditor
{
    // 提供 Unity Inspector 的编辑期选择代理，不进入运行时领域。
    public class InspectorPreviewAsset : ScriptableObject
    {
        private System.Func<Presentation.Presenters.PropertyPresenter> createPresenter;
        // 代理由所属宿主注入工厂，每个Inspector独立持有输入草稿。
        public void Bind(System.Func<Presentation.Presenters.PropertyPresenter> createPresenter)
            => this.createPresenter = createPresenter ?? throw new System.ArgumentNullException(nameof(createPresenter));
        public Presentation.Presenters.PropertyPresenter CreatePresenter() => createPresenter?.Invoke();
    }
}