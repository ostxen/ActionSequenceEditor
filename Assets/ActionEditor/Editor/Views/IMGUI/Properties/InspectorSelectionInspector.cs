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
        private NBC.ActionEditor.Presentation.Properties.PropertyTargetState sequenceTarget, selectionTarget;
        private PropertyInspectorBase sequenceView, selectionView;
        private bool expanded = true;

        // 域重载恢复的Editor可能仍指向旧类型或已销毁代理，不能强制转换。
        private void OnEnable() => presenter = target is InspectorSelectionTarget proxy && proxy != null ? proxy.CreatePresenter() : null;
        private void OnDisable()
        {
            sequenceView?.CancelInput(); selectionView?.CancelInput(); presenter?.CancelDraft();
            sequenceView = selectionView = null; sequenceTarget = selectionTarget = null; presenter = null;
        }
        protected override void OnHeaderGUI() => GUILayout.Space(18);

        public override void OnInspectorGUI()
        {
            if (presenter != null)
                foreach (var issue in presenter.CaptureIssues())
                    if (GUILayout.Button(issue.Message)) presenter.SelectIssue(issue);
            var nextSelectionTarget = presenter?.CaptureSelectionTarget();
            var nextSequenceTarget = presenter?.CaptureSequenceTarget();
            if (nextSelectionTarget == null)
            {
                sequenceView?.CancelInput(); selectionView?.CancelInput();
                sequenceView = selectionView = null; sequenceTarget = selectionTarget = null;
                EditorGUILayout.HelpBox(EditorText.NoSelection, MessageType.Info); return;
            }
            if (!ReferenceEquals(sequenceTarget?.Id.ViewKey, nextSequenceTarget?.Id.ViewKey))
            {
                sequenceView?.CancelInput(); selectionView?.CancelInput();
                sequenceView = nextSequenceTarget == null ? null : EditorExtensionFactory.CreateInspector(nextSequenceTarget, presenter);
                selectionTarget = null; selectionView = null;
            }
            if (!ReferenceEquals(selectionTarget?.Id.ViewKey, nextSelectionTarget.Id.ViewKey))
            {
                selectionView?.CancelInput();
                selectionView = EditorExtensionFactory.CreateInspector(nextSelectionTarget, presenter);
            }
            sequenceTarget = nextSequenceTarget; selectionTarget = nextSelectionTarget;
            sequenceView?.SetTarget(sequenceTarget); selectionView?.SetTarget(selectionTarget);
            var color = GUI.color;
            var richText = GUI.skin.label.richText;
            try
            {
                GUI.skin.GetStyle("label").richText = true;
                GUILayout.Space(5);
                using (new EditorGUI.DisabledScope(selectionTarget.Locked))
                {
                    expanded = EditorGUILayout.Foldout(expanded, string.Format(EditorText.InsBaseInfo, sequenceTarget.Title), true);
                    if (expanded) sequenceView?.OnInspectorGUI();
                    EditorGUIControls.BoldSeparator();
                    if (selectionTarget != null)
                    {
                        EditorGUILayout.LabelField(selectionTarget.Title, EditorStyles.boldLabel);
                        var description = selectionTarget.Description;
                        if (!string.IsNullOrEmpty(description)) EditorGUILayout.HelpBox(description, MessageType.None);
                    }
                    selectionView?.OnInspectorGUI();
                }
            }
            finally { GUI.color = color; GUI.skin.label.richText = richText; }
            Repaint();
        }
    }
}