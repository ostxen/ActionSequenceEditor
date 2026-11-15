using NBC.ActionEditor.Views.IMGUI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Views.IMGUI
{
    // 处理原生选择列表、搜索和预览布局；资源发现与Editor所有权各自独立。
    public class ObjectSelectorWindow : EditorWindow
    {
        private class Styles
        {
            public GUIStyle smallStatus = "ObjectPickerSmallStatus";
            public GUIStyle largeStatus = "ObjectPickerLargeStatus";
            public GUIStyle toolbarBack = "ObjectPickerToolbar";
            public GUIStyle tab = "ObjectPickerTab";
            public GUIStyle bottomResize = "WindowBottomResize";
            public GUIStyle background = "ObjectPickerBackground";
            public GUIStyle previewBackground = "PopupCurveSwatchBackground";
            public GUIStyle previewTextureBackground = "ObjectPickerPreviewBackground";

            public GUIStyle resultsGridLabel = "ProjectBrowserGridLabel";
            public GUIStyle resultsLabel = "PR Label";
            public GUIStyle iconAreaBg = "ProjectBrowserIconAreaBg";
            public GUIStyle previewBg = "ProjectBrowserPreviewBg";

            public GUIStyle preButton = "preButton";
            public GUIStyle preToolbar = "preToolbar";
            public GUIStyle dragHandle = "RL DragHandle";
        }

        private Styles _styles;
        private float _toolbarHeight = 44f;
        private float _previewSize = 101f;
        private float _topSize;
        private string _searchFilter;
        private bool _focusSearchFilter;
        private int _selectedIndex = -1;
        private ObjectPickerCatalog.Entry[] _entries = Array.Empty<ObjectPickerCatalog.Entry>();
        private ObjectPickerCatalog.Entry[] _visibleEntries = Array.Empty<ObjectPickerCatalog.Entry>();
        private string _folderPath;
        private Object _selectedObject;
        private Texture2D _selectedIcon;
        private Action<Object> _itemSelectedCallback;
        private Predicate<string> _assetPathFilter;
        private ObjectPickerCatalog.Entry _noneEntry;
        private bool _showNoneItem;
        private Vector2 _scrollPosition;
        private ObjectPreviewCache _previewCache;
        private PreviewResizer _previewResizer = new PreviewResizer();
        private static ObjectSelectorWindow _sharedObjectSelector;

        static string SearchField(Rect position, string text)
        {
            Rect position2 = position;
            position2.width -= 15f;
            text = EditorGUI.TextField(position2, text, new GUIStyle("SearchTextField"));
            Rect position3 = position;
            position3.x += position.width - 15f;
            position3.width = 15f;
            if (GUI.Button(position3, GUIContent.none,
                    string.IsNullOrEmpty(text) ? "SearchCancelButtonEmpty" : "SearchCancelButton"))
            {
                text = string.Empty;
                GUIUtility.keyboardControl = 0;
            }

            return text;
        }

        private static ObjectSelectorWindow Shared
        {
            get
            {
                if (_sharedObjectSelector == null)
                {
                    Object[] array = Resources.FindObjectsOfTypeAll(typeof(ObjectSelectorWindow));
                    if (array != null && array.Length > 0)
                    {
                        _sharedObjectSelector = (ObjectSelectorWindow)array[0];
                    }

                    if (_sharedObjectSelector == null)
                    {
                        _sharedObjectSelector = CreateInstance<ObjectSelectorWindow>();
                    }
                }

                return _sharedObjectSelector;
            }
        }

        /// <summary>
        /// 列表项绘制区域
        /// </summary>
        private Rect listPosition =>
            new Rect(0f, _toolbarHeight, position.width, Mathf.Max(0f, _topSize - _toolbarHeight));

        /// <summary>
        /// 显示的列表项数量
        /// </summary>
        private int itemCount => _visibleEntries.Length + (_showNoneItem ? 1 : 0);

        /// <summary>
        /// 显示对象选择器
        /// </summary>
        /// <param name="obj">初始的对象</param>
        /// <param name="itemSelectedCallback">列表项选中回调</param>
        /// <param name="folderPath">所属的文件夹路径</param>
        public static void ShowObjectPicker<T>(Object obj, Action<Object> itemSelectedCallback,
            string folderPath = "Assets", List<int> allowedInstanceIDs = null, Predicate<string> assetPathFilter = null) where T : Object
        {
            Shared.ShowPicker(obj, typeof(T).Name, itemSelectedCallback, folderPath, allowedInstanceIDs, assetPathFilter);
        }

        // 发现和选择回调都沿本次打开参数建立，不持有作者字段或事务。
        private void ShowPicker(Object obj, string requiredType,
            Action<Object> itemSelectedCallback, string folderPath, List<int> allowedInstanceIDs = null, Predicate<string> assetPathFilter = null)
        {
            var fullPath = Path.Combine(UnityEngine.Application.dataPath, folderPath.Length > 6 ? folderPath.Substring(7) : string.Empty);
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError(folderPath + " is not a Directory!");
                return;
            }

            _assetPathFilter = assetPathFilter;
            _folderPath = folderPath;
            DiscoverEntries(requiredType, allowedInstanceIDs);
            _itemSelectedCallback = itemSelectedCallback;
            InitIfNeeded();
            titleContent = new GUIContent("Select " + requiredType);
            _focusSearchFilter = true;
            _showNoneItem = true;
            _searchFilter = string.Empty;
            _scrollPosition = Vector2.zero;
            _previewCache?.Dispose();
            _previewCache = null;
            SelectIndex(0, false);
            FrameSelection(obj, true);
            ShowAuxWindow();
        }

        private void DiscoverEntries(string requiredType, List<int> allowedInstanceIDs)
            => _entries = _visibleEntries = ObjectPickerCatalog.Discover(_folderPath, requiredType, allowedInstanceIDs, _assetPathFilter);

        private void OnEnable()
        {
            _previewResizer.Init("ObjectSelectorWindow");
            _previewSize = _previewResizer.GetPreviewSize();
        }

        private void OnDisable()
        {
            _itemSelectedCallback = null;
            _assetPathFilter = null;
            _entries = Array.Empty<ObjectPickerCatalog.Entry>();
            _visibleEntries = Array.Empty<ObjectPickerCatalog.Entry>();
            _selectedObject = null;
            _selectedIcon = null;
            if (_sharedObjectSelector == this)
            {
                _sharedObjectSelector = null;
            }

            if (_previewCache != null)
            {
                _previewCache.Dispose();
                _previewCache = null;
            }
        }

        private void OnGUI()
        {
            OnObjectListGUI();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Cancel();
            }
        }

        private void InitIfNeeded()
        {
            if (_styles == null)
            {
                _styles = new Styles();
            }

            if (_noneEntry == null)
            {
                _noneEntry = new ObjectPickerCatalog.Entry();
                _noneEntry.name = "None";
            }

            _topSize = position.height - _previewSize;
        }

        private void Cancel()
        {
            Close();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private void OnObjectListGUI()
        {
            InitIfNeeded();
            ResizeBottomPartOfWindow();
            HandleKeyboard();
            GUI.BeginGroup(new Rect(0f, 0f, position.width, position.height), GUIContent.none);
            SearchArea();
            ListArea();
            PreviewArea();
            GUI.EndGroup();

            GUI.Label(new Rect(position.width * 0.5f - 16f,
                    position.height - _previewSize + 2f, 32f,
                    _styles.bottomResize.fixedHeight),
                GUIContent.none, _styles.bottomResize);
        }

        private void SearchArea()
        {
            GUI.Label(new Rect(0f, 0f, position.width, _toolbarHeight), GUIContent.none, _styles.toolbarBack);
            bool flag = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            GUI.SetNextControlName("SearchFilter");
            string text = SearchField(new Rect(5f, 5f, position.width - 10f, 15f), _searchFilter);
            if (flag && Event.current.type == EventType.Used)
            {
                if (_searchFilter == string.Empty)
                {
                    Cancel();
                }

                _focusSearchFilter = true;
            }

            if (text != _searchFilter || _focusSearchFilter)
            {
                _searchFilter = text;
                ApplySearch();
                Repaint();
            }

            if (_focusSearchFilter)
            {
                EditorGUI.FocusTextInControl("SearchFilter");
                _focusSearchFilter = false;
            }

            GUILayout.BeginArea(new Rect(0f, 26f, position.width, _toolbarHeight - 26f));
            GUILayout.BeginHorizontal();
            GUILayout.Toggle(true, _folderPath, _styles.tab);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ListArea()
        {
            Rect totalRect = listPosition;
            float itemHeight = itemCount * 16f;
            Rect viewRect = new Rect(0f, 0f, 1f, itemHeight);
            GUI.Label(totalRect, GUIContent.none, _styles.iconAreaBg);
            _scrollPosition = GUI.BeginScrollView(totalRect, _scrollPosition, viewRect);

            int firstRow = FirstVisibleRow(0f, _scrollPosition);
            if (firstRow >= 0 && firstRow < itemCount)
            {
                const float rowHeight = 16f;
                int visibleRows = (int)Math.Ceiling(totalRect.height / rowHeight);
                int endRow = Math.Min(itemCount, firstRow + visibleRows + 1);
                DrawRows(firstRow, endRow);
            }

            GUI.EndScrollView();

            if (_selectedObject && !_selectedIcon &&
                AssetPreview.IsLoadingAssetPreview(_selectedObject.GetInstanceID()))
            {
                _selectedIcon = AssetPreview.GetAssetPreview(_selectedObject);
                Repaint();
            }
        }

        private void DrawRows(int beginIndex, int endIndex)
        {
            for (var row = beginIndex; row < endIndex; row++)
                DrawRow(RowRect(row), _showNoneItem && row == 0 ? _noneEntry : _visibleEntries[row - (_showNoneItem ? 1 : 0)], row);
        }

        private void DrawRow(Rect rect4, ObjectPickerCatalog.Entry builtinResource, int itemIdx)
        {
            Event current = Event.current;
            float num5 = 18f;
            Rect rect5 = new Rect(num5, rect4.y, rect4.width - num5, rect4.height);
            bool selected = false;
            bool focus = true;

            if (current.type == EventType.MouseDown)
            {
                if (current.button == 0 && rect4.Contains(current.mousePosition))
                {
                    if (current.clickCount == 1)
                    {
                        SelectIndex(itemIdx);
                        current.Use();
                    }
                    else if (current.clickCount == 2)
                    {
                        current.Use();
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            else if (current.type == EventType.Repaint)
            {
                if (itemIdx == _selectedIndex)
                {
                    _styles.resultsLabel.Draw(rect4, GUIContent.none, false, false, true, focus);
                }

                _styles.resultsLabel.Draw(rect5, builtinResource.name, false, false, selected, focus);
                Rect rect6 = rect5;
                rect6.width = 16f;
                rect6.x = 16f;
                if (builtinResource.icon != null)
                {
                    GUI.DrawTexture(rect6, builtinResource.icon);
                }
            }
        }

        /// <summary>
        /// 每个项的矩形区域
        /// </summary>
        private Rect RowRect(int itemIdx)
        {
            return new Rect(0f, itemIdx * 16f, listPosition.width, 16f);
        }

        private void PreviewArea()
        {
            GUI.Box(new Rect(0f, _topSize, position.width, _previewSize), string.Empty, _styles.previewBackground);

            if (_previewCache == null)
            {
                _previewCache = new ObjectPreviewCache();
            }

            Object currentObject = _selectedObject;
            ObjectPreviewEditor editorWrapper = null;

            if (_previewSize < 75f)
            {
                string text;
                if (currentObject != null)
                {
                    editorWrapper = _previewCache[currentObject];
                    string str = ObjectNames.NicifyVariableName(currentObject.GetType().Name);
                    if (editorWrapper != null)
                    {
                        text = editorWrapper.name + " (" + str + ")";
                    }
                    else
                    {
                        text = currentObject.name + " (" + str + ")";
                    }

                    text = text + "      " + AssetDatabase.GetAssetPath(currentObject);
                }
                else
                {
                    text = "None";
                }

                LinePreview(text, currentObject, editorWrapper);
            }
            else
            {
                string text3;
                if (currentObject != null)
                {
                    editorWrapper = _previewCache[currentObject];
                    string text2 = ObjectNames.NicifyVariableName(currentObject.GetType().Name);
                    if (editorWrapper != null)
                    {
                        text3 = editorWrapper.GetInfoString().Replace("\n", "  ");
                        if (text3 != string.Empty)
                        {
                            text3 = string.Concat(new string[]
                            {
                                editorWrapper.name,
                                "\n",
                                text2,
                                "\n",
                                text3
                            });
                        }
                        else
                        {
                            text3 = editorWrapper.name + "\n" + text2;
                        }
                    }
                    else
                    {
                        text3 = currentObject.name + "\n" + text2;
                    }

                    text3 = text3 + "\n" + AssetDatabase.GetAssetPath(currentObject);
                }
                else
                {
                    text3 = "None";
                }

                WidePreview(_previewSize, text3, currentObject, editorWrapper);
            }
            _previewCache.CleanupUntouchedEditors();
        }

        private void LinePreview(string s, Object o, ObjectPreviewEditor p)
        {
            if (_selectedIcon != null)
            {
                GUI.DrawTexture(new Rect(2f, (int)(_topSize + 2f), 16f, 16f), _selectedIcon,
                    ScaleMode.StretchToFill);
            }

            Rect pos = new Rect(20f, _topSize + 1f, position.width - 22f, 18f);
            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DropShadowLabel(pos, s, _styles.smallStatus);
            }
            else
            {
                GUI.Label(pos, s, _styles.smallStatus);
            }
        }

        private void WidePreview(float actualSize, string s, Object o, ObjectPreviewEditor p)
        {
            float num = 5f;
            Rect pos = new Rect(num, _topSize + num, actualSize - num * 2f, actualSize - num * 2f);
            Rect position2 = new Rect(_previewSize + 3f, _topSize + (_previewSize - 75f) * 0.5f,
                position.width - _previewSize - 3f - num, 75f);
            if (p != null && p.HasPreviewGUI())
            {
                p.OnInteractivePreviewGUI(pos, _styles.previewTextureBackground);

                Rect rect = new Rect(_previewSize + 3f, position.height - 22f, position.width, 16f);
                GUI.BeginGroup(rect);
                EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.Height(17f));
                p.OnPreviewSettings();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUI.EndGroup();
            }
            else
            {
                if (o != null)
                {
                    DrawObjectIcon(pos, _selectedIcon);
                }
            }

            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DropShadowLabel(position2, s, _styles.smallStatus);
            }
            else
            {
                GUI.Label(position2, s, _styles.smallStatus);
            }
        }

        private void ResizeBottomPartOfWindow()
        {
            GUI.changed = false;
            float x = 5f + _previewSize - 5f * 2f;
            Rect dragRect = new Rect(x, 0, position.width - x, 0f);
            _previewSize = _previewResizer.ResizeHandle(position, 65f, 270f, 20f, dragRect) + 20f;
            _topSize = position.height - _previewSize;
        }

        private void HandleKeyboard()
        {
            if (!GUI.enabled || Event.current.type != EventType.KeyDown)
            {
                return;
            }

            switch (Event.current.keyCode)
            {
                case KeyCode.UpArrow:
                    if (_selectedIndex > 0)
                    {
                        SelectIndex(_selectedIndex - 1);
                    }
                    Event.current.Use();
                    break;
                case KeyCode.DownArrow:
                    if (_selectedIndex < itemCount - 1)
                    {
                        SelectIndex(_selectedIndex + 1);
                    }
                    Event.current.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_selectedIndex >= 0 && _selectedIndex < itemCount)
                    {
                        Event.current.Use();
                        Close();
                        GUIUtility.ExitGUI();
                    }
                    break;
            }
        }

        /// <summary>
        /// 设置选中的索引
        /// </summary>
        private void SelectIndex(int selectedIdx, bool callback = true)
        {
            if (selectedIdx < 0 || selectedIdx >= itemCount) return;
            _selectedIndex = selectedIdx;
            var row = RowRect(selectedIdx);

            if (_showNoneItem && selectedIdx == 0)
            {
                _selectedObject = null;
                _selectedIcon = null;
            }
            else
            {
                if (_showNoneItem)
                {
                    selectedIdx--;
                }

                if (_visibleEntries[selectedIdx].id > 0)
                {
                    _selectedObject = EditorUtility.InstanceIDToObject(_visibleEntries[selectedIdx].id);
                }
                else
                {
                    _selectedObject =
                        AssetDatabase.LoadAssetAtPath<Object>(_visibleEntries[selectedIdx].path);
                }

                _selectedIcon = AssetPreview.GetAssetPreview(_selectedObject);

                if (_previewCache != null && _selectedObject)
                {
                    _previewCache.CleanupUntouchedEditors();
                    _ = _previewCache[_selectedObject];
                }
            }

            ScrollToPosition(AdjustRectForFraming(row));
            Repaint();

            if (callback && _itemSelectedCallback != null)
            {
                if (_selectedObject == null || _assetPathFilter == null || _assetPathFilter(AssetDatabase.GetAssetPath(_selectedObject)))
                    _itemSelectedCallback(_selectedObject);
            }
        }

        /// <summary>
        /// 绘制对象的预览图标
        /// </summary>
        private void DrawObjectIcon(Rect rect, Texture icon)
        {
            if (icon == null)
            {
                return;
            }

            int num = Mathf.Min((int)rect.width, (int)rect.height);
            if (num >= icon.width * 2)
            {
                num = icon.width * 2;
            }

            FilterMode filterMode = icon.filterMode;
            icon.filterMode = FilterMode.Point;
            GUI.DrawTexture(
                new Rect(rect.x + ((int)rect.width - num) / 2, rect.y + ((int)rect.height - num) / 2, num, num), icon,
                ScaleMode.ScaleToFit);
            icon.filterMode = filterMode;
        }

        /// <summary>
        /// 找到第一个可见的列表项
        /// </summary>
        private int FirstVisibleRow(float yOffset, Vector2 scrollPos)
        {
            float num = scrollPos.y - yOffset;
            int result = 0;
            if (num > 0f)
            {
                float num2 = 16f; // 列表项高度
                result = (int)Mathf.Max(0f, Mathf.Floor(num / num2));
            }

            return result;
        }

        /// <summary>
        /// 搜索字符串变化
        /// </summary>
        private void ApplySearch()
        {
            var matches = new List<ObjectPickerCatalog.Entry>();
            foreach (var entry in _entries)
                if (string.IsNullOrEmpty(_searchFilter) || entry.name.IndexOf(_searchFilter, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    matches.Add(entry);
            _visibleEntries = matches.ToArray();
            _selectedIndex = _selectedObject == null ? 0 : IndexOfPath(AssetDatabase.GetAssetPath(_selectedObject));
            if (_selectedIndex >= 0) ScrollToPosition(AdjustRectForFraming(RowRect(_selectedIndex)));
        }

        /// <summary>
        /// 列表项滚动到指定矩形区域
        /// </summary>
        private void ScrollToPosition(Rect r)
        {
            float y = r.y;
            float yMax = r.yMax;
            float height = listPosition.height;
            if (yMax > height + _scrollPosition.y)
            {
                _scrollPosition.y = yMax - height;
            }

            if (y < _scrollPosition.y)
            {
                _scrollPosition.y = y;
            }

            _scrollPosition.y = Mathf.Max(_scrollPosition.y, 0f);
        }

        /// <summary>
        /// 指定矩形定位后的区域
        /// </summary>
        private Rect AdjustRectForFraming(Rect r)
        {
            r.height += _styles.resultsGridLabel.fixedHeight * 2f;
            r.y -= _styles.resultsGridLabel.fixedHeight;
            return r;
        }

        /// <summary>
        /// 列表项定位
        /// </summary>
        private bool FrameSelection(string assetPath, bool frame)
        {
            int num = IndexOfPath(assetPath);
            if (num != -1)
            {
                if (frame)
                {
                    SelectIndex(num, false);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 列表项定位
        /// </summary>
        private bool FrameSelection(Object obj, bool frame)
        {
            if (obj == null || !AssetDatabase.Contains(obj))
            {
                return false;
            }

            return FrameSelection(AssetDatabase.GetAssetPath(obj), frame);
        }

        /// <summary>
        /// 根据路径查找所在的索引
        /// </summary>
        private int IndexOfPath(string assetPath)
        {
            int num = 0;
            if (_showNoneItem)
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    return 0;
                }

                num++;
            }
            else
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    return -1;
                }
            }

            ObjectPickerCatalog.Entry[] activeBuiltinList = _visibleEntries;
            for (int j = 0; j < activeBuiltinList.Length; j++)
            {
                ObjectPickerCatalog.Entry builtinResource = activeBuiltinList[j];
                if (assetPath == builtinResource.path)
                {
                    return num;
                }

                num++;
            }

            return -1;
        }
    }


}