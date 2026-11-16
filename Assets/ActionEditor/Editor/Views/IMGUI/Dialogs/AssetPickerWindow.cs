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
    public class AssetPickerWindow : EditorWindow
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
        private float _previewHeight = 101f;
        private float _listBottomY;
        private string _searchFilter;
        private bool _focusSearchFilter;
        private int _selectedIndex = -1;
        private AssetPickerDiscovery.Entry[] _entries = Array.Empty<AssetPickerDiscovery.Entry>();
        private AssetPickerDiscovery.Entry[] _visibleEntries = Array.Empty<AssetPickerDiscovery.Entry>();
        private string _folderPath;
        private Object _selectedObject;
        private Texture2D _selectedIcon;
        private Action<Object> _selectionChanged;
        private Predicate<string> _assetPathFilter;
        private AssetPickerDiscovery.Entry _noneEntry;
        private bool _showNoneItem;
        private Vector2 _scrollPosition;
        private ObjectPreviewCache _previewCache;
        private PreviewResizer _previewResizer = new PreviewResizer();
        private static AssetPickerWindow _sharedPicker;

        static string SearchField(Rect position, string text)
        {
            Rect inputRect = position;
            inputRect.width -= 15f;
            text = EditorGUI.TextField(inputRect, text, new GUIStyle("SearchTextField"));
            Rect clearButtonRect = position;
            clearButtonRect.x += position.width - 15f;
            clearButtonRect.width = 15f;
            if (GUI.Button(clearButtonRect, GUIContent.none,
                    string.IsNullOrEmpty(text) ? "SearchCancelButtonEmpty" : "SearchCancelButton"))
            {
                text = string.Empty;
                GUIUtility.keyboardControl = 0;
            }

            return text;
        }

        private static AssetPickerWindow Shared
        {
            get
            {
                if (_sharedPicker == null)
                {
                    Object[] existingPickers = Resources.FindObjectsOfTypeAll(typeof(AssetPickerWindow));
                    if (existingPickers != null && existingPickers.Length > 0)
                    {
                        _sharedPicker = (AssetPickerWindow)existingPickers[0];
                    }

                    if (_sharedPicker == null)
                    {
                        _sharedPicker = CreateInstance<AssetPickerWindow>();
                    }
                }

                return _sharedPicker;
            }
        }

        /// <summary>
        /// 列表项绘制区域
        /// </summary>
        private Rect ListRect =>
            new Rect(0f, _toolbarHeight, position.width, Mathf.Max(0f, _listBottomY - _toolbarHeight));

        /// <summary>
        /// 显示的列表项数量
        /// </summary>
        private int VisibleRowCount => _visibleEntries.Length + (_showNoneItem ? 1 : 0);

        /// <summary>
        /// 显示对象选择器
        /// </summary>
        /// <param name="obj">初始的对象</param>
        /// <param name="itemSelectedCallback">列表项选中回调</param>
        /// <param name="folderPath">所属的文件夹路径</param>
        public static void ShowObjectPicker<T>(Object obj, Action<Object> selectionChanged,
            string folderPath = "Assets", List<int> allowedInstanceIDs = null, Predicate<string> assetPathFilter = null) where T : Object
        {
            Shared.ShowPicker(obj, typeof(T).Name, selectionChanged, folderPath, allowedInstanceIDs, assetPathFilter);
        }

        // 发现和选择回调都沿本次打开参数建立，不持有作者字段或事务。
        private void ShowPicker(Object obj, string requiredType,
            Action<Object> selectionChanged, string folderPath, List<int> allowedInstanceIDs = null, Predicate<string> assetPathFilter = null)
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
            _selectionChanged = selectionChanged;
            InitIfNeeded();
            titleContent = new GUIContent("Select " + requiredType);
            _focusSearchFilter = true;
            _showNoneItem = true;
            _searchFilter = string.Empty;
            _scrollPosition = Vector2.zero;
            _previewCache?.Dispose();
            _previewCache = null;
            SelectIndex(0, false);
            FindAndSelect(obj, true);
            ShowAuxWindow();
        }

        private void DiscoverEntries(string requiredType, List<int> allowedInstanceIDs)
            => _entries = _visibleEntries = AssetPickerDiscovery.Discover(_folderPath, requiredType, allowedInstanceIDs, _assetPathFilter);

        private void OnEnable()
        {
            _previewResizer.Init("ObjectSelectorWindow");
            _previewHeight = _previewResizer.GetPreviewSize();
        }

        private void OnDisable()
        {
            _selectionChanged = null;
            _assetPathFilter = null;
            _entries = Array.Empty<AssetPickerDiscovery.Entry>();
            _visibleEntries = Array.Empty<AssetPickerDiscovery.Entry>();
            _selectedObject = null;
            _selectedIcon = null;
            if (_sharedPicker == this)
            {
                _sharedPicker = null;
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
                ClosePicker();
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
                _noneEntry = new AssetPickerDiscovery.Entry();
                _noneEntry.name = "None";
            }

            _listBottomY = position.height - _previewHeight;
        }

        private void ClosePicker()
        {
            Close();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private void OnObjectListGUI()
        {
            InitIfNeeded();
            HandlePreviewResize();
            HandleKeyboard();
            GUI.BeginGroup(new Rect(0f, 0f, position.width, position.height), GUIContent.none);
            SearchArea();
            ListArea();
            PreviewArea();
            GUI.EndGroup();

            GUI.Label(new Rect(position.width * 0.5f - 16f,
                    position.height - _previewHeight + 2f, 32f,
                    _styles.bottomResize.fixedHeight),
                GUIContent.none, _styles.bottomResize);
        }

        private void SearchArea()
        {
            GUI.Label(new Rect(0f, 0f, position.width, _toolbarHeight), GUIContent.none, _styles.toolbarBack);
            bool escapePressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            GUI.SetNextControlName("SearchFilter");
            string searchText = SearchField(new Rect(5f, 5f, position.width - 10f, 15f), _searchFilter);
            if (escapePressed && Event.current.type == EventType.Used)
            {
                if (_searchFilter == string.Empty)
                {
                    ClosePicker();
                }

                _focusSearchFilter = true;
            }

            if (searchText != _searchFilter || _focusSearchFilter)
            {
                _searchFilter = searchText;
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
            Rect totalRect = ListRect;
            float contentHeight = VisibleRowCount * 16f;
            Rect viewRect = new Rect(0f, 0f, 1f, contentHeight);
            GUI.Label(totalRect, GUIContent.none, _styles.iconAreaBg);
            _scrollPosition = GUI.BeginScrollView(totalRect, _scrollPosition, viewRect);

            int firstRow = FirstVisibleRow(0f, _scrollPosition);
            if (firstRow >= 0 && firstRow < VisibleRowCount)
            {
                const float rowHeight = 16f;
                int visibleRows = (int)Math.Ceiling(totalRect.height / rowHeight);
                int endRow = Math.Min(VisibleRowCount, firstRow + visibleRows + 1);
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

        private void DrawRows(int firstRow, int endRowExclusive)
        {
            for (var row = firstRow; row < endRowExclusive; row++)
                DrawRow(RowRect(row), _showNoneItem && row == 0 ? _noneEntry : _visibleEntries[row - (_showNoneItem ? 1 : 0)], row);
        }

        private void DrawRow(Rect rowRect, AssetPickerDiscovery.Entry entry, int rowIndex)
        {
            Event current = Event.current;
            float contentLeft = 18f;
            Rect contentRect = new Rect(contentLeft, rowRect.y, rowRect.width - contentLeft, rowRect.height);
            bool contentSelected = false;
            bool contentFocused = true;

            if (current.type == EventType.MouseDown)
            {
                if (current.button == 0 && rowRect.Contains(current.mousePosition))
                {
                    if (current.clickCount == 1)
                    {
                        SelectIndex(rowIndex);
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
                if (rowIndex == _selectedIndex)
                {
                    _styles.resultsLabel.Draw(rowRect, GUIContent.none, false, false, true, contentFocused);
                }

                _styles.resultsLabel.Draw(contentRect, entry.name, false, false, contentSelected, contentFocused);
                Rect iconRect = contentRect;
                iconRect.width = 16f;
                iconRect.x = 16f;
                if (entry.icon != null)
                {
                    GUI.DrawTexture(iconRect, entry.icon);
                }
            }
        }

        /// <summary>
        /// 每个项的矩形区域
        /// </summary>
        private Rect RowRect(int rowIndex)
        {
            return new Rect(0f, rowIndex * 16f, ListRect.width, 16f);
        }

        private void PreviewArea()
        {
            GUI.Box(new Rect(0f, _listBottomY, position.width, _previewHeight), string.Empty, _styles.previewBackground);

            if (_previewCache == null)
            {
                _previewCache = new ObjectPreviewCache();
            }

            Object currentObject = _selectedObject;
            ObjectPreviewEditor editorWrapper = null;

            if (_previewHeight < 75f)
            {
                string summaryText;
                if (currentObject != null)
                {
                    editorWrapper = _previewCache[currentObject];
                    string typeDisplayName = ObjectNames.NicifyVariableName(currentObject.GetType().Name);
                    if (editorWrapper != null)
                    {
                        summaryText = editorWrapper.name + " (" + typeDisplayName + ")";
                    }
                    else
                    {
                        summaryText = currentObject.name + " (" + typeDisplayName + ")";
                    }

                    summaryText = summaryText + "      " + AssetDatabase.GetAssetPath(currentObject);
                }
                else
                {
                    summaryText = "None";
                }

                DrawCompactPreview(summaryText, currentObject, editorWrapper);
            }
            else
            {
                string detailsText;
                if (currentObject != null)
                {
                    editorWrapper = _previewCache[currentObject];
                    string typeDisplayName = ObjectNames.NicifyVariableName(currentObject.GetType().Name);
                    if (editorWrapper != null)
                    {
                        detailsText = editorWrapper.GetInfoString().Replace("\n", "  ");
                        if (detailsText != string.Empty)
                        {
                            detailsText = string.Concat(new string[]
                            {
                                editorWrapper.name,
                                "\n",
                                typeDisplayName,
                                "\n",
                                detailsText
                            });
                        }
                        else
                        {
                            detailsText = editorWrapper.name + "\n" + typeDisplayName;
                        }
                    }
                    else
                    {
                        detailsText = currentObject.name + "\n" + typeDisplayName;
                    }

                    detailsText = detailsText + "\n" + AssetDatabase.GetAssetPath(currentObject);
                }
                else
                {
                    detailsText = "None";
                }

                DrawExpandedPreview(_previewHeight, detailsText, currentObject, editorWrapper);
            }
            _previewCache.CleanupUntouchedEditors();
        }

        private void DrawCompactPreview(string summaryText, Object o, ObjectPreviewEditor p)
        {
            if (_selectedIcon != null)
            {
                GUI.DrawTexture(new Rect(2f, (int)(_listBottomY + 2f), 16f, 16f), _selectedIcon,
                    ScaleMode.StretchToFill);
            }

            Rect summaryRect = new Rect(20f, _listBottomY + 1f, position.width - 22f, 18f);
            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DropShadowLabel(summaryRect, summaryText, _styles.smallStatus);
            }
            else
            {
                GUI.Label(summaryRect, summaryText, _styles.smallStatus);
            }
        }

        private void DrawExpandedPreview(float previewHeight, string detailsText, Object asset, ObjectPreviewEditor previewEditor)
        {
            float padding = 5f;
            Rect previewRect = new Rect(padding, _listBottomY + padding, previewHeight - padding * 2f, previewHeight - padding * 2f);
            Rect detailsRect = new Rect(_previewHeight + 3f, _listBottomY + (_previewHeight - 75f) * 0.5f,
                position.width - _previewHeight - 3f - padding, 75f);
            if (previewEditor != null && previewEditor.HasPreviewGUI())
            {
                previewEditor.OnInteractivePreviewGUI(previewRect, _styles.previewTextureBackground);

                Rect settingsRect = new Rect(_previewHeight + 3f, position.height - 22f, position.width, 16f);
                GUI.BeginGroup(settingsRect);
                EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.Height(17f));
                previewEditor.OnPreviewSettings();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUI.EndGroup();
            }
            else
            {
                if (asset != null)
                {
                    DrawObjectIcon(previewRect, _selectedIcon);
                }
            }

            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DropShadowLabel(detailsRect, detailsText, _styles.smallStatus);
            }
            else
            {
                GUI.Label(detailsRect, detailsText, _styles.smallStatus);
            }
        }

        private void HandlePreviewResize()
        {
            GUI.changed = false;
            float x = 5f + _previewHeight - 5f * 2f;
            Rect dragRect = new Rect(x, 0, position.width - x, 0f);
            _previewHeight = _previewResizer.ResizeHandle(position, 65f, 270f, 20f, dragRect) + 20f;
            _listBottomY = position.height - _previewHeight;
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
                    if (_selectedIndex < VisibleRowCount - 1)
                    {
                        SelectIndex(_selectedIndex + 1);
                    }
                    Event.current.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_selectedIndex >= 0 && _selectedIndex < VisibleRowCount)
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
        private void SelectIndex(int rowIndex, bool notifySelectionChanged = true)
        {
            if (rowIndex < 0 || rowIndex >= VisibleRowCount) return;
            _selectedIndex = rowIndex;
            var row = RowRect(rowIndex);

            if (_showNoneItem && rowIndex == 0)
            {
                _selectedObject = null;
                _selectedIcon = null;
            }
            else
            {
                if (_showNoneItem)
                {
                    rowIndex--;
                }

                if (_visibleEntries[rowIndex].id > 0)
                {
                    _selectedObject = EditorUtility.InstanceIDToObject(_visibleEntries[rowIndex].id);
                }
                else
                {
                    _selectedObject =
                        AssetDatabase.LoadAssetAtPath<Object>(_visibleEntries[rowIndex].path);
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

            if (notifySelectionChanged && _selectionChanged != null)
            {
                if (_selectedObject == null || _assetPathFilter == null || _assetPathFilter(AssetDatabase.GetAssetPath(_selectedObject)))
                    _selectionChanged(_selectedObject);
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

            int iconSize = Mathf.Min((int)rect.width, (int)rect.height);
            if (iconSize >= icon.width * 2)
            {
                iconSize = icon.width * 2;
            }

            FilterMode filterMode = icon.filterMode;
            icon.filterMode = FilterMode.Point;
            GUI.DrawTexture(
                new Rect(rect.x + ((int)rect.width - iconSize) / 2, rect.y + ((int)rect.height - iconSize) / 2, iconSize, iconSize), icon,
                ScaleMode.ScaleToFit);
            icon.filterMode = filterMode;
        }

        /// <summary>
        /// 找到第一个可见的列表项
        /// </summary>
        private int FirstVisibleRow(float yOffset, Vector2 scrollPos)
        {
            float scrolledHeight = scrollPos.y - yOffset;
            int result = 0;
            if (scrolledHeight > 0f)
            {
                float rowHeight = 16f; // 列表项高度
                result = (int)Mathf.Max(0f, Mathf.Floor(scrolledHeight / rowHeight));
            }

            return result;
        }

        /// <summary>
        /// 搜索字符串变化
        /// </summary>
        private void ApplySearch()
        {
            var matches = new List<AssetPickerDiscovery.Entry>();
            foreach (var entry in _entries)
                if (string.IsNullOrEmpty(_searchFilter) || entry.name.IndexOf(_searchFilter, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    matches.Add(entry);
            _visibleEntries = matches.ToArray();
            _selectedIndex = _selectedObject == null ? 0 : FindRowIndexByPath(AssetDatabase.GetAssetPath(_selectedObject));
            if (_selectedIndex >= 0) ScrollToPosition(AdjustRectForFraming(RowRect(_selectedIndex)));
        }

        /// <summary>
        /// 列表项滚动到指定矩形区域
        /// </summary>
        private void ScrollToPosition(Rect rowRect)
        {
            float y = rowRect.y;
            float yMax = rowRect.yMax;
            float height = ListRect.height;
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
        private Rect AdjustRectForFraming(Rect rowRect)
        {
            rowRect.height += _styles.resultsGridLabel.fixedHeight * 2f;
            rowRect.y -= _styles.resultsGridLabel.fixedHeight;
            return rowRect;
        }

        /// <summary>
        /// 列表项定位
        /// </summary>
        private bool FindAndSelect(string assetPath, bool selectMatch)
        {
            int rowIndex = FindRowIndexByPath(assetPath);
            if (rowIndex != -1)
            {
                if (selectMatch)
                {
                    SelectIndex(rowIndex, false);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 列表项定位
        /// </summary>
        private bool FindAndSelect(Object obj, bool selectMatch)
        {
            if (obj == null || !AssetDatabase.Contains(obj))
            {
                return false;
            }

            return FindAndSelect(AssetDatabase.GetAssetPath(obj), selectMatch);
        }

        /// <summary>
        /// 根据路径查找所在的索引
        /// </summary>
        private int FindRowIndexByPath(string assetPath)
        {
            int rowIndex = 0;
            if (_showNoneItem)
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    return 0;
                }

                rowIndex++;
            }
            else
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    return -1;
                }
            }

            AssetPickerDiscovery.Entry[] visibleEntries = _visibleEntries;
            for (int j = 0; j < visibleEntries.Length; j++)
            {
                AssetPickerDiscovery.Entry entry = visibleEntries[j];
                if (assetPath == entry.path)
                {
                    return rowIndex;
                }

                rowIndex++;
            }

            return -1;
        }
    }


}