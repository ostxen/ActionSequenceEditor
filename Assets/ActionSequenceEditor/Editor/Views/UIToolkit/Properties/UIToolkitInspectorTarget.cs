using System;
using UnityEngine;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 作为Unity Inspector的逐窗选择目标，不保存作者数据。
    public sealed class UIToolkitInspectorTarget : ScriptableObject
    {
        [NonSerialized] public ActionSequenceEditorWindow Owner;
    }
}
