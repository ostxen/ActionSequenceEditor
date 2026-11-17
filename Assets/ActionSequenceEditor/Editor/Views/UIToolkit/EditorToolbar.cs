using ActionSequenceEditor.Model;
using ActionSequenceEditor.Presentation.Timeline;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 播放与文档工具栏拥有自己的控件和同步，不访问窗口私有状态。
    internal sealed class EditorToolbar : VisualElement
    {
        private readonly Composition.WindowBinding binding;
        private readonly Action<Action> Run;
        private readonly VisualElement transport, toolbar, customHeader;
        private readonly ToolbarButton sourcePicker;
        private readonly Label lastSave;
        private readonly ToolbarToggle magnet;
        private Type headerSequenceType;
        private PlaybackPresenter Playback => binding.Playback;
        private SequencePresenter Sequence => binding.Sequence;
        public EditorToolbar(Composition.WindowBinding binding, Action<Action> run, Action<string> showError, Action showSettings)
        {
            this.binding = binding; Run = run;
            name = "timeline-header"; style.height = ActionSequenceEditorWindow.HeaderHeight;
            style.flexShrink = 0; style.flexDirection = FlexDirection.Row;
            transport = new VisualElement { name = "timeline-transport" }; transport.style.flexDirection = FlexDirection.Row; transport.style.flexShrink = 0;
            void Control(string text, string hint, Action click, Texture2D icon)
            {
                var button = new ToolbarButton(() => Run(click)) { text = text, tooltip = hint };
                button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.marginLeft = 0; button.style.marginRight = 0;
                button.style.height = 40;
                if (icon != null)
                {
                    button.text = ""; var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.style.width = Length.Percent(100); image.style.height = Length.Percent(100); button.Add(image);
                }
                transport.Add(button);
            }
            Control("‹", "返回资源选择", () => Sequence.Invoke(SequenceIntent.Close), Styles.BackIcon);
            Control("|◀", "开始", () => Playback.Invoke(PlaybackIntent.First), Styles.FirstFrameIcon); Control("◀", "上一帧", () => Playback.Invoke(PlaybackIntent.Previous), Styles.PrevFrameIcon);
            Control("▶/■", "播放/停止", () => Playback.Invoke(PlaybackIntent.TogglePlay), Styles.PlayIcon);
            Control("Ⅱ", "暂停/继续", () => Playback.Invoke(PlaybackIntent.TogglePause), Styles.PauseIcon); Control("▶|", "下一帧", () => Playback.Invoke(PlaybackIntent.Next), Styles.NextFrameIcon);
            Control("▶|", "结束", () => Playback.Invoke(PlaybackIntent.Last), Styles.LastFrameIcon);
            Control("↔", "范围循环", () => Playback.Invoke(PlaybackIntent.ToggleRange), Styles.RangeIcon);
            Add(transport);
            var right = new VisualElement(); right.style.flexGrow = 1; right.style.minWidth = 0; right.style.marginLeft = Styles.SplitterWidth;
            toolbar = new Toolbar { name = "timeline-document-toolbar" }; toolbar.style.height = 20;
            sourcePicker = new ToolbarButton(() => Run(() => Sequence.Invoke(SequenceIntent.Open)))
                { text = "[]", name = "timeline-document-picker", tooltip = EditorText.OpenSequenceTooltip };
            sourcePicker.AddToClassList("unity-toolbar-menu"); toolbar.Add(sourcePicker);
            var dropdownArrow = new Label("▼") { pickingMode = PickingMode.Ignore }; dropdownArrow.style.position = Position.Absolute;
            dropdownArrow.style.right = 4; dropdownArrow.style.top = 3; dropdownArrow.style.fontSize = 8; sourcePicker.Add(dropdownArrow);
            customHeader = new VisualElement { name = "timeline-custom-header" }; customHeader.style.flexDirection = FlexDirection.Row; toolbar.Add(customHeader);
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; toolbar.Add(spacer);
            lastSave = new Label { name = "timeline-last-save" }; lastSave.style.fontSize = 11; lastSave.style.color = new Color(1, 1, 1, .3f); lastSave.style.unityTextAlign = TextAnchor.MiddleLeft; toolbar.Add(lastSave);
            ToolbarButton IconButton(string name, Texture2D texture, string hint, Action action)
            {
                var button = new ToolbarButton(() => Run(action)) { name = name, tooltip = hint }; button.style.width = 26;
                button.style.flexShrink = 0; button.Add(new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }); toolbar.Add(button); return button;
            }
            var saveButton = IconButton("timeline-save", Styles.SaveIcon, EditorText.SaveMenuHint, () => Sequence.Invoke(SequenceIntent.Save));
            saveButton.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 1) return;
                var menu = new GenericDropdownMenu();
                binding.PopulateSaveMenu((label, action) => menu.AddItem(label, false, () => Run(action)));
                TimelineDropdown.DropDown(menu, saveButton.worldBound, saveButton); evt.StopPropagation();
            });
            magnet = new ToolbarToggle { name = "timeline-magnet", value = binding.CurrentSettings.MagnetSnapping, tooltip = EditorText.OpenMagnetSnappingTips };
            magnet.style.width = 26; magnet.style.flexShrink = 0; magnet.Add(new Image { image = Styles.MagnetIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
            magnet.RegisterValueChangedCallback(e =>
            {
                binding.Settings.Change(magnetSnapping: e.newValue);
                showError(binding.SettingsError);
            }); toolbar.Add(magnet);
            IconButton("timeline-settings", Styles.SettingsIcon, EditorText.OpenSettingsTooltip, showSettings);
            right.Add(toolbar); var rulerSpace = new VisualElement(); rulerSpace.style.height = 20; right.Add(rulerSpace); Add(right);
        }
        public void RefreshSequence()
        {
                var sourceName = binding.SourceName;
                sourcePicker.text = "[" + sourceName + "]";
                sourcePicker.style.minWidth = 80;
                if (headerSequenceType != Sequence.SequenceType)
                {
                    headerSequenceType = Sequence.SequenceType; customHeader.Clear();
                    var adapter = !Sequence.HasSequence ? null : EditorExtensionFactory.CreateToolkitHeader(Sequence.SequenceType);
                    var header = adapter?.CreateGUI(binding.Extensions);
                    if (header != null) customHeader.Add(header);
                }
        }
        public void Sync(float width)
        {
            transport.style.width = width;
            var playImage = transport.Children().ElementAt(3).Q<Image>(); if (playImage != null) playImage.image = Playback.Playing ? Styles.StopIcon : Styles.PlayIcon;
            foreach (var index in new[] { 3, 4, 7 })
                transport.Children().ElementAt(index).style.backgroundColor = (index == 3 ? Playback.Playing : index == 4 ? Playback.Paused : Playback.RangePlaybackEnabled) ? new Color(.22f, .38f, .55f) : StyleKeyword.Null;
            lastSave.text = string.Format(EditorText.HeaderLastSaveTime, binding.LastSave.ToString("HH:mm:ss"));
            magnet.SetValueWithoutNotify(binding.CurrentSettings.MagnetSnapping);
        }
    }
}
