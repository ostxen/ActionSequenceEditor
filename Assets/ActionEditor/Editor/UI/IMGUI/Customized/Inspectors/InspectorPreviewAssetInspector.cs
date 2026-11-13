using System;
using NBC.ActionEditor.Presentation.Presenters;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    // Inspector桥只拥有本次选中对象的原生适配，图替换后释放旧引用。
    [CustomEditor(typeof(InspectorPreviewAsset))]
    public class InspectorPreviewAssetInspector : Editor
    {
        private PropertyPresenter presenter;
        private object document, selected;
        private InspectorsBase documentView, selectedView;
        private bool expanded = true;

        private void OnEnable() => presenter = ((InspectorPreviewAsset)target).CreatePresenter();
        private void OnDisable()
        {
            documentView?.CancelInput(); selectedView?.CancelInput(); presenter?.CancelDraft();
            documentView = selectedView = null; document = selected = null; presenter = null;
        }
        protected override void OnHeaderGUI() => GUILayout.Space(18);

        public override void OnInspectorGUI()
        {
            if (presenter == null || presenter.Selected == null)
            {
                documentView?.CancelInput(); selectedView?.CancelInput();
                documentView = selectedView = null; document = selected = null;
                EditorGUILayout.HelpBox(Lan.NotSelectAsset, MessageType.Info); return;
            }
            if (!ReferenceEquals(document, presenter.Document))
            {
                documentView?.CancelInput(); selectedView?.CancelInput();
                document = presenter.Document;
                documentView = document is IData data ? EditorCustomFactory.GetInspector(data, presenter) : null;
                selected = null; selectedView = null;
            }
            if (!ReferenceEquals(selected, presenter.Selected))
            {
                selectedView?.CancelInput();
                selected = presenter.Selected;
                selectedView = selected is IData data ? EditorCustomFactory.GetInspector(data, presenter) : null;
            }
            var color = GUI.color;
            var richText = GUI.skin.label.richText;
            try
            {
                GUI.skin.GetStyle("label").richText = true;
                GUILayout.Space(5);
                using (new EditorGUI.DisabledScope(presenter.Locked(selected)))
                {
                    expanded = EditorGUILayout.Foldout(expanded, string.Format(Lan.InsBaseInfo, presenter.Title(document)), true);
                    if (expanded) documentView?.OnInspectorGUI();
                    EditorTools.BoldSeparator();
                    if (selected != null)
                    {
                        EditorGUILayout.LabelField(presenter.Title(selected), EditorStyles.boldLabel);
                        var description = presenter.Description(selected);
                        if (!string.IsNullOrEmpty(description)) EditorGUILayout.HelpBox(description, MessageType.None);
                    }
                    selectedView?.OnInspectorGUI();
                }
            }
            finally { GUI.color = color; GUI.skin.label.richText = richText; }
            Repaint();
        }
    }
}