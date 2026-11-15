using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Views.IMGUI.Controls;
using System;
using NBC.ActionEditor.Presentation.Presenters;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Properties
{
    // Inspector桥只拥有本次选中对象的原生适配，图替换后释放旧引用。
    [CustomEditor(typeof(InspectorSelectionTarget))]
    public class InspectorSelectionInspector : Editor
    {
        private PropertyPresenter presenter;
        private NBC.ActionEditor.Presentation.Properties.PropertyTargetState document, selected;
        private InspectorsBase documentView, selectedView;
        private bool expanded = true;

        // 域重载恢复的Editor可能仍指向旧类型或已销毁代理，不能强制转换。
        private void OnEnable() => presenter = target is InspectorSelectionTarget proxy && proxy != null ? proxy.CreatePresenter() : null;
        private void OnDisable()
        {
            documentView?.CancelInput(); selectedView?.CancelInput(); presenter?.CancelDraft();
            documentView = selectedView = null; document = selected = null; presenter = null;
        }
        protected override void OnHeaderGUI() => GUILayout.Space(18);

        public override void OnInspectorGUI()
        {
            if (presenter != null)
                foreach (var issue in presenter.CaptureIssues())
                    if (GUILayout.Button(issue.Message)) presenter.SelectIssue(issue);
            var nextSelected = presenter?.CaptureSelectionTarget();
            var nextDocument = presenter?.CaptureDocumentTarget();
            if (nextSelected == null)
            {
                documentView?.CancelInput(); selectedView?.CancelInput();
                documentView = selectedView = null; document = selected = null;
                EditorGUILayout.HelpBox(Lan.NotSelectAsset, MessageType.Info); return;
            }
            if (!ReferenceEquals(document?.Id.ViewKey, nextDocument?.Id.ViewKey))
            {
                documentView?.CancelInput(); selectedView?.CancelInput();
                documentView = nextDocument == null ? null : EditorExtensionFactory.GetInspector(nextDocument, presenter);
                selected = null; selectedView = null;
            }
            if (!ReferenceEquals(selected?.Id.ViewKey, nextSelected.Id.ViewKey))
            {
                selectedView?.CancelInput();
                selectedView = EditorExtensionFactory.GetInspector(nextSelected, presenter);
            }
            document = nextDocument; selected = nextSelected;
            documentView?.SetTarget(document); selectedView?.SetTarget(selected);
            var color = GUI.color;
            var richText = GUI.skin.label.richText;
            try
            {
                GUI.skin.GetStyle("label").richText = true;
                GUILayout.Space(5);
                using (new EditorGUI.DisabledScope(selected.Locked))
                {
                    expanded = EditorGUILayout.Foldout(expanded, string.Format(Lan.InsBaseInfo, document.Title), true);
                    if (expanded) documentView?.OnInspectorGUI();
                    EditorTools.BoldSeparator();
                    if (selected != null)
                    {
                        EditorGUILayout.LabelField(selected.Title, EditorStyles.boldLabel);
                        var description = selected.Description;
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