using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 列表、搜索和可调整预览均为原生控件；不创建IMGUI预览Editor。
    public sealed class SequencePickerWindow : DialogWindowBase
    {
        private ISequenceDialogCatalog catalog;
        private Action<object> selectionChanged;
        private AssetPickerDiscovery.Entry[] entries;
        private readonly List<AssetPickerDiscovery.Entry> visibleEntries = new List<AssetPickerDiscovery.Entry>();
        private ListView assetList;
        private Image previewImage;
        private Label detailsLabel;
        private TextField textPreviewField;
        private Object selectedAsset;
        private IVisualElementScheduledItem previewPoll;
        public static void ShowFor(ISequenceDialogCatalog catalog, Action<object> selectionChanged)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (selectionChanged == null) throw new ArgumentNullException(nameof(selectionChanged));
            var window = CreateInstance<SequencePickerWindow>(); window.catalog = catalog; window.selectionChanged = selectionChanged;
            window.minSize = new Vector2(360, 300); window.ShowUtility();
        }
        public void CreateGUI()
        {
            if (selectionChanged == null) { LostBinding(); return; }
            Begin(EditorText.OpenSequence);
            entries = AssetPickerDiscovery.Discover("Assets/", "TextAsset", null, catalog.IsDocumentPath);
            var search = new ToolbarSearchField { name = "document-search" };
            rootVisualElement.Add(search);
            var split = new TwoPaneSplitView(1, 110, TwoPaneSplitViewOrientation.Vertical) { name = "document-preview-split" };
            split.style.flexGrow = 1;
            assetList = new ListView { name = "document-list", itemsSource = visibleEntries, fixedItemHeight = 20, selectionType = SelectionType.Single };
            assetList.makeItem = () =>
            {
                var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
                var icon = new Image(); icon.style.width = icon.style.height = 18; row.Add(icon); row.Add(new Label()); return row;
            };
            assetList.bindItem = (row, index) =>
            {
                var entry = visibleEntries[index]; row.Q<Label>().text = entry?.name ?? "None";
                row.Q<Image>().image = entry?.icon; row.tooltip = entry?.path;
            };
            assetList.selectionChanged += values =>
            {
                var choices = values.Cast<AssetPickerDiscovery.Entry>().ToArray();
                if (choices.Length > 0) Run(() => Select(choices[0]));
            };
            assetList.itemsChosen += _ => Close();
            split.Add(assetList);
            var pane = new VisualElement { name = "document-preview" }; pane.style.minHeight = 40;
            pane.style.flexDirection = FlexDirection.Row;
            previewImage = new Image { scaleMode = ScaleMode.ScaleToFit }; previewImage.style.width = 96; previewImage.style.flexShrink = 0;
            detailsLabel = new Label("None"); detailsLabel.style.whiteSpace = WhiteSpace.Normal; detailsLabel.style.flexGrow = 1;
            var info = new VisualElement(); info.style.flexGrow = 1; info.style.minWidth = 0;
            textPreviewField = new TextField { name = "document-text-preview", multiline = true, isReadOnly = true };
            textPreviewField.style.flexGrow = 1; textPreviewField.style.minHeight = 0; textPreviewField.style.display = DisplayStyle.None;
            info.Add(detailsLabel); info.Add(textPreviewField); pane.Add(previewImage); pane.Add(info); split.Add(pane); rootVisualElement.Add(split);
            Finish(EditorText.TipsConfirm, Close);
            search.RegisterValueChangedCallback(evt => Filter(evt.newValue));
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
                {
                    assetList.selectedIndex = Mathf.Clamp(assetList.selectedIndex + (evt.keyCode == KeyCode.DownArrow ? 1 : -1), 0, visibleEntries.Count - 1);
                    assetList.ScrollToItem(assetList.selectedIndex); evt.StopPropagation(); evt.PreventDefault();
                }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                { evt.StopPropagation(); Close(); }
            }, TrickleDown.TrickleDown);
            Filter(""); assetList.SetSelectionWithoutNotify(new[] { 0 });
            previewPoll = rootVisualElement.schedule.Execute(UpdatePreview).Every(100);
            search.schedule.Execute(search.Focus);
        }
        private void Filter(string query)
        {
            visibleEntries.Clear(); visibleEntries.Add(null);
            visibleEntries.AddRange(entries.Where(entry => string.IsNullOrEmpty(query) || entry.name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0));
            assetList.Rebuild();
            var index = selectedAsset == null ? 0 : visibleEntries.FindIndex(entry => entry != null && entry.path == AssetDatabase.GetAssetPath(selectedAsset));
            assetList.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Array.Empty<int>());
            if (index >= 0) assetList.ScrollToItem(index);
        }
        private void Select(AssetPickerDiscovery.Entry entry)
        {
            selectedAsset = entry == null ? null : AssetDatabase.LoadAssetAtPath<Object>(entry.path);
            detailsLabel.text = selectedAsset == null ? "None" : selectedAsset.name + "\n" + ObjectNames.NicifyVariableName(selectedAsset.GetType().Name) + "\n" + entry.path;
            textPreviewField.SetValueWithoutNotify(selectedAsset is TextAsset asset ? asset.text : "");
            textPreviewField.style.display = selectedAsset is TextAsset ? DisplayStyle.Flex : DisplayStyle.None;
            UpdatePreview();
            if (selectedAsset == null || catalog.IsDocumentPath(AssetDatabase.GetAssetPath(selectedAsset))) selectionChanged(selectedAsset);
        }
        private void UpdatePreview()
        {
            if (previewImage == null) return;
            previewImage.image = selectedAsset == null ? null : AssetPreview.GetAssetPreview(selectedAsset) ?? AssetPreview.GetMiniThumbnail(selectedAsset);
        }
        private void OnDisable()
        {
            previewPoll?.Pause(); previewPoll = null;
            selectionChanged = null; catalog = null; entries = null; visibleEntries.Clear(); selectedAsset = null; previewImage = null; detailsLabel = null; textPreviewField = null; assetList = null;
            rootVisualElement.Clear();
        }
    }
}
