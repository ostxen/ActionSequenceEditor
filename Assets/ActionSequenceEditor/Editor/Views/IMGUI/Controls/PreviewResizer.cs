using System;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI.Controls
{
    // 处理选择器预览区的原生尺寸拖动与折叠偏好。
    [Serializable]
    internal class PreviewResizer
    {
        private static float s_DraggedPreviewSize;
        private static float s_CurrentPreviewHeight;
        private static float s_MouseDownY;
        private static float s_ValueAtMouseDown;
        private static bool s_MouseDragged;
        [SerializeField] private float m_CachedPref;
        [SerializeField] private int m_ControlHash;
        [SerializeField] private string m_PrefName;
        private int _controlId;

        private int ControlId
        {
            get
            {
                if (_controlId == 0)
                {
                    _controlId = GUIUtility.GetControlID(m_ControlHash, FocusType.Passive, default(Rect));
                }

                return _controlId;
            }
        }

        public void Init(string prefName)
        {
            if (m_ControlHash != 0 && !string.IsNullOrEmpty(m_PrefName))
            {
                return;
            }

            m_ControlHash = prefName.GetHashCode();
            m_PrefName = "Preview_" + prefName;
            m_CachedPref = EditorPrefs.GetFloat(m_PrefName, 1f);
        }

        public float ResizeHandle(Rect windowRect, float minimumPreviewHeight, float minimumContentHeight, float handleHeight)
        {
            return ResizeHandle(windowRect, minimumPreviewHeight, minimumContentHeight, handleHeight, default(Rect));
        }

        public float ResizeHandle(Rect windowRect, float minimumPreviewHeight, float minimumContentHeight, float handleHeight,
            Rect horizontalDragBounds)
        {
            if (Mathf.Abs(m_CachedPref) < minimumPreviewHeight)
            {
                m_CachedPref = minimumPreviewHeight * Mathf.Sign(m_CachedPref);
            }

            float maximumPreviewHeight = windowRect.height - minimumContentHeight;
            bool isDragging = GUIUtility.hotControl == ControlId;
            float previewHeight = (!isDragging) ? Mathf.Max(0f, m_CachedPref) : s_DraggedPreviewSize;
            bool expanded = m_CachedPref > 0f;
            float rememberedExpandedHeight = Mathf.Abs(m_CachedPref);
            Rect handleRect = new Rect(0f, windowRect.height - previewHeight - handleHeight, windowRect.width,
                handleHeight);
            if (horizontalDragBounds.width != 0f)
            {
                handleRect.x = horizontalDragBounds.x;
                handleRect.width = horizontalDragBounds.width;
            }

            bool wasExpanded = expanded;
            previewHeight = -PixelPreciseCollapsibleSlider(ControlId, handleRect, -previewHeight, -maximumPreviewHeight, 0f, ref expanded);
            previewHeight = Mathf.Min(previewHeight, maximumPreviewHeight);
            isDragging = (GUIUtility.hotControl == ControlId);
            if (isDragging)
            {
                s_DraggedPreviewSize = previewHeight;
            }

            if (previewHeight < minimumPreviewHeight)
            {
                previewHeight = ((previewHeight >= minimumPreviewHeight * 0.5f) ? minimumPreviewHeight : 0f);
            }

            if (expanded != wasExpanded)
            {
                previewHeight = ((!expanded) ? 0f : rememberedExpandedHeight);
            }

            expanded = (previewHeight >= minimumPreviewHeight / 2f);
            if (GUIUtility.hotControl == 0)
            {
                if (previewHeight > 0f)
                {
                    rememberedExpandedHeight = previewHeight;
                }

                float signedPreviewHeight = rememberedExpandedHeight * (float)((!expanded) ? -1 : 1);
                if (signedPreviewHeight != m_CachedPref)
                {
                    m_CachedPref = signedPreviewHeight;
                    EditorPrefs.SetFloat(m_PrefName, m_CachedPref);
                }
            }

            s_CurrentPreviewHeight = previewHeight;
            return previewHeight;
        }

        public bool GetExpanded()
        {
            if (GUIUtility.hotControl == ControlId)
            {
                return s_CurrentPreviewHeight > 0f;
            }

            return m_CachedPref > 0f;
        }

        public float GetPreviewSize()
        {
            if (GUIUtility.hotControl == ControlId)
            {
                return Mathf.Max(0f, s_CurrentPreviewHeight);
            }

            return Mathf.Max(0f, m_CachedPref);
        }

        public bool GetExpandedBeforeDragging()
        {
            return m_CachedPref > 0f;
        }

        public void SetExpanded(bool expanded)
        {
            m_CachedPref = Mathf.Abs(m_CachedPref) * (float)((!expanded) ? -1 : 1);
            EditorPrefs.SetFloat(m_PrefName, m_CachedPref);
        }

        public void ToggleExpanded()
        {
            m_CachedPref = -m_CachedPref;
            EditorPrefs.SetFloat(m_PrefName, m_CachedPref);
        }

        public static float PixelPreciseCollapsibleSlider(int id, Rect position, float value, float min, float max,
            ref bool expanded)
        {
            Event current = Event.current;
            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (GUIUtility.hotControl == 0 && current.button == 0 && position.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        s_MouseDownY = current.mousePosition.y;
                        s_ValueAtMouseDown = value;
                        s_MouseDragged = false;
                        current.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        if (!s_MouseDragged)
                        {
                            expanded = !expanded;
                        }

                        current.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = Mathf.Clamp(
                            current.mousePosition.y - s_MouseDownY +
                            s_ValueAtMouseDown, min, max - 1f);
                        GUI.changed = true;
                        s_MouseDragged = true;
                        current.Use();
                    }

                    break;
                case EventType.Repaint:
                    if (GUIUtility.hotControl == 0)
                    {
                        EditorGUIUtility.AddCursorRect(position, MouseCursor.SplitResizeUpDown);
                    }

                    if (GUIUtility.hotControl == id)
                    {
                        EditorGUIUtility.AddCursorRect(
                            new Rect(position.x, position.y - 100f, position.width, position.height + 200f),
                            MouseCursor.SplitResizeUpDown);
                    }

                    break;
            }

            return value;
        }
    }
}