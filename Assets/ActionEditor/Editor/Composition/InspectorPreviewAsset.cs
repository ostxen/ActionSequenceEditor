using UnityEngine;

namespace NBC.ActionEditor
{
    // 提供 Unity Inspector 的编辑期选择代理，不进入运行时领域。
    public class InspectorPreviewAsset : ScriptableObject
    {
        // 每个Inspector创建自己的输入Presenter，共用宿主作者会话。
        public Presentation.Presenters.PropertyPresenter CreatePresenter() => Composition.PropertyComposition.Create(App.Context);
    }
}