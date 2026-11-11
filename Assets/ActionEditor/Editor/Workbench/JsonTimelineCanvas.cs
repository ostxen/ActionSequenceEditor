using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    public sealed class JsonTimelineCanvas : VisualElement
    {
        private readonly JsonActionWorkbench owner;
        private readonly VisualElement ruler;
        private readonly VisualElement dragGuides;
        private readonly VisualElement[] guideLines = new VisualElement[2];
        private float[] snapTimes = Array.Empty<float>();
        private bool dragMoved;
        private readonly Button create, open, addGroup;
        private readonly VisualElement welcome, rowHeaders;
        private readonly VisualElement clipLabels;
        private readonly Dictionary<Clip, Label> labels = new Dictionary<Clip, Label>();
        private int pointer = -1;
        private JsonEditTransaction edit;
        private Vector2 down, last;
        private Clip hit;
        private Track movingTrack;
        private enum GestureMode { None, Move, ResizeLeft, ResizeRight, Pan, Splitter, Marquee, Seek, Track }
        private GestureMode mode;
        private static bool EditsClips(GestureMode value) => value == GestureMode.Move || value == GestureMode.ResizeLeft || value == GestureMode.ResizeRight;
        private float seekBefore;
        private readonly Dictionary<Clip, Vector2> original = new Dictionary<Clip, Vector2>();
        private Rect? marquee;
        private Vector2 hoverPosition;
        private bool hasHoverPosition;
        public float Left => owner.LeftWidth + 9;
        private Asset Asset => owner.Document;
        private JsonTimelineTransform Transform => new JsonTimelineTransform(Asset?.ViewTimeMin ?? 0, Asset?.ViewTime ?? 7, owner.rootVisualElement.contentRect.width - Left, Left);
        public float Scale => Transform.Scale;
        public float X(float time) => Transform.X(time);
        public float TimeAt(float x) => Transform.Time(x);
        public JsonTimelineCanvas(JsonActionWorkbench owner)
        {
            this.owner = owner; name = "json-timeline"; focusable = true;
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ActionEditor/Editor/Workbench/JsonTimelineCanvas.uss"));
            style.flexGrow = 1; style.minWidth = 0; style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
            // Ruler occupies the second half of the original 40px header, above the scrolling content.
            ruler = new VisualElement { name = "json-ruler", pickingMode = PickingMode.Ignore };
            ruler.style.position = Position.Absolute; ruler.style.left = 0; ruler.style.top = 20; ruler.style.right = 0; ruler.style.height = 20;
            ruler.generateVisualContent += DrawRuler;
            dragGuides = new VisualElement { name = "json-drag-guides", pickingMode = PickingMode.Ignore };
            dragGuides.style.position = Position.Absolute; dragGuides.style.top = 20; dragGuides.style.bottom = 16; dragGuides.style.right = 0;
            dragGuides.style.overflow = Overflow.Hidden;
            for (var i = 0; i < guideLines.Length; i++)
            {
                var guide = new VisualElement { name = "json-drag-guide-" + i, pickingMode = PickingMode.Ignore };
                guide.style.position = Position.Absolute; guide.style.top = guide.style.bottom = 0; guide.style.width = 1;
                guide.generateVisualContent += context =>
                {
                    var painter = context.painter2D; var height = guide.contentRect.height;
                    if (guide.ClassListContains("json-guide-solid")) Line(painter, Vector2.zero, new Vector2(0, height), Color.white);
                    else
                    {
                        var count = Mathf.FloorToInt(height / 10);
                        for (var dash = 0; dash < count; dash++)
                            Line(painter, new Vector2(0, dash * height / count), new Vector2(0, (dash + .5f) * height / count), new Color(.48f, .48f, .48f));
                    }
                };
                var label = new Label { pickingMode = PickingMode.Ignore }; label.style.position = Position.Absolute; label.style.top = 0;
                label.style.height = 18; label.style.fontSize = 12; label.style.color = Color.white; label.style.backgroundColor = new Color(0, 0, 0, .8f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.marginLeft = label.style.marginTop = 0;
                guide.Add(label); guideLines[i] = guide; dragGuides.Add(guide);
            }
            create = new Button(() => CreateAssetWindow.Show()) { text = "创建 JSON 资源" };
            open = new Button(() => ObjectSelectorWindow.ShowObjectPicker<TextAsset>(null, App.OnObjectPickerConfig, "Assets/", assetPathFilter: App.IsJsonDocumentPath)) { text = "打开 JSON 资源" };
            welcome = new VisualElement { name = "json-welcome" }; welcome.style.position = Position.Absolute;
            welcome.style.left = welcome.style.right = welcome.style.top = welcome.style.bottom = 0;
            welcome.style.alignItems = Align.Center; welcome.style.justifyContent = Justify.Center;
            if (Styles.Logo != null) { var logo = new Image { image = Styles.Logo }; logo.style.width = logo.style.height = 128; welcome.Add(logo); }
            var title = new Label("行为时间轴编辑器"); title.style.fontSize = 30; title.style.unityFontStyleAndWeight = FontStyle.Bold; welcome.Add(title);
            var hint = new Label("同一份 JSON，可在 GUILayout 与 UI Toolkit 界面编辑"); hint.style.marginTop = 20; welcome.Add(hint);
            foreach (var button in new[] { create, open }) { button.style.width = 220; button.style.height = 40; welcome.Add(button); }
            create.style.backgroundColor = new Color(.56f, .56f, .63f);
            welcome.Add(new Label("Open Workbench 使用 UI Toolkit，数据与本窗口共用"));
            var preferences = new Button(() => JsonPreferencesWindow.ShowFor(owner)) { text = Lan.Seeting }; preferences.style.width = 220; preferences.style.height = 40; welcome.Add(preferences); Add(welcome);
            rowHeaders = new VisualElement { name = "json-row-headers", pickingMode = PickingMode.Ignore }; rowHeaders.style.position = Position.Absolute; rowHeaders.style.left = rowHeaders.style.top = 0; Add(rowHeaders);
            clipLabels = new VisualElement { pickingMode = PickingMode.Ignore }; clipLabels.style.position = Position.Absolute; clipLabels.style.top = clipLabels.style.right = clipLabels.style.bottom = 0;
            clipLabels.style.overflow = Overflow.Hidden; Add(clipLabels);
            addGroup = new Button(ShowGroupMenu) { text = Lan.GroupAdd, name = "json-add-group" };
            addGroup.style.position = Position.Absolute; addGroup.style.left = 15; addGroup.style.height = 24; Add(addGroup);
            addGroup.style.marginLeft = addGroup.style.marginRight = addGroup.style.marginTop = addGroup.style.marginBottom = 0;
            RegisterCallback<PointerDownEvent>(Down); RegisterCallback<PointerMoveEvent>(MoveSafely); RegisterCallback<PointerUpEvent>(Up);
            RegisterCallback<PointerLeaveEvent>(_ => { hasHoverPosition = false; UpdateResizeCursor(); });
            RegisterCallback<PointerCaptureOutEvent>(_ => Cancel()); RegisterCallback<DetachFromPanelEvent>(_ => { Cancel(); ruler.RemoveFromHierarchy(); dragGuides.RemoveFromHierarchy(); });
            RegisterCallback<KeyDownEvent>(Key);
            RegisterCallback<WheelEvent>(Wheel); this.AddManipulator(new ContextualMenuManipulator(Menu));
            // Header ruler input is captured at the root, not by an IMGUI overlay.
            owner.rootVisualElement.RegisterCallback<PointerDownEvent>(HeaderDown);
            owner.rootVisualElement.RegisterCallback<PointerMoveEvent>(HeaderMove);
            owner.rootVisualElement.RegisterCallback<PointerUpEvent>(HeaderUp);
            owner.rootVisualElement.RegisterCallback<KeyDownEvent>(RootKey, TrickleDown.TrickleDown);
        }
        public void AttachRuler() { owner.rootVisualElement.Add(ruler); owner.rootVisualElement.Add(dragGuides); SyncDragGuides(); }
        private void HeaderDown(PointerDownEvent e)
        {
            var p = owner.rootVisualElement.WorldToLocal(e.position);
            if (Asset != null && e.button == 0 && p.y >= 20 && p.y < 40 && p.x >= Left)
            {
                Focus();
                seekBefore = AssetPlayer.Inst.CurrentTime; mode = GestureMode.Seek; pointer = e.pointerId;
                this.CapturePointer(pointer); owner.Seek(Snap(TimeAt(p.x))); e.PreventDefault(); e.StopPropagation();
            }
        }
        private void HeaderMove(PointerMoveEvent e)
        {
            if (mode != GestureMode.Seek || pointer != e.pointerId) return;
            var p = owner.rootVisualElement.WorldToLocal(e.position); owner.Seek(Snap(TimeAt(p.x))); e.StopPropagation();
        }
        private void HeaderUp(PointerUpEvent e) { if (mode == GestureMode.Seek) Up(e); }
        private static bool TextInputTarget(IEventHandler target)
        {
            for (var element = target as VisualElement; element != null; element = element.parent)
                if (element.ClassListContains("unity-base-text-field")) return true;
            return false;
        }
        private void RootKey(KeyDownEvent e) { if (!TextInputTarget(e.target) && e.keyCode == KeyCode.Escape) { Cancel(); e.StopPropagation(); } }
        public void Dispose()
        {
            Cancel(); owner.rootVisualElement.UnregisterCallback<PointerDownEvent>(HeaderDown);
            owner.rootVisualElement.UnregisterCallback<PointerMoveEvent>(HeaderMove); owner.rootVisualElement.UnregisterCallback<PointerUpEvent>(HeaderUp); ruler.RemoveFromHierarchy();
            owner.rootVisualElement.UnregisterCallback<KeyDownEvent>(RootKey, TrickleDown.TrickleDown);
            dragGuides.RemoveFromHierarchy();
        }
        public void RefreshVisualState()
        {
            welcome.style.display = Asset == null ? DisplayStyle.Flex : DisplayStyle.None;
            ruler.style.display = Asset == null ? DisplayStyle.None : DisplayStyle.Flex;
            RebuildHeaders();
            labels.Clear(); clipLabels.Clear();
            foreach (var clip in Rows().OfType<Track>().SelectMany(t => t.Clips).Where(c => c.Length > 0))
            {
                var label = new Label(clip.GetName()) { pickingMode = PickingMode.Ignore }; label.style.position = Position.Absolute;
                label.style.unityFontStyleAndWeight = FontStyle.Bold; label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.fontSize = 12;
                label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
                labels.Add(clip, label); clipLabels.Add(label);
            }
            addGroup.style.display = Asset == null ? DisplayStyle.None : DisplayStyle.Flex;
            MarkDirtyRepaint(); ruler.MarkDirtyRepaint();
        }
        public void SyncAddGroup()
        {
            addGroup.style.top = Rows().Count * 28 - owner.ScrollY + 2; addGroup.style.width = Mathf.Max(60, owner.LeftWidth - 30);
            rowHeaders.style.top = -owner.ScrollY;
            foreach (var row in rowHeaders.Children()) row.style.width = owner.LeftWidth;
            clipLabels.style.left = Left;
            var rows = Rows();
            foreach (var pair in labels)
            {
                var rect = ClipRect(pair.Key, rows.IndexOf(pair.Key.Parent)); var label = pair.Value;
                label.style.left = rect.x - Left; label.style.top = rect.y; label.style.width = rect.width; label.style.height = 22;
                var size = EditorStyles.label.CalcSize(new GUIContent(label.text));
                label.style.display = rect.width > size.x ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UpdateResizeCursor();
            SyncDragGuides();
        }
        private void SyncDragGuides()
        {
            dragGuides.style.left = Left;
            var visible = dragMoved && EditsClips(mode) && original.Count > 0;
            dragGuides.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            var start = original.Keys.Min(c => c.StartTime); var end = original.Keys.Max(c => c.EndTime);
            for (var i = 0; i < guideLines.Length; i++)
            {
                var guide = guideLines[i]; var time = i == 0 ? start : end;
                guide.style.display = (mode == GestureMode.ResizeLeft && i == 1 || mode == GestureMode.ResizeRight && i == 0) ? DisplayStyle.None : DisplayStyle.Flex;
                guide.style.left = X(time) - Left;
                guide.EnableInClassList("json-guide-solid", snapTimes.Any(t => Mathf.Abs(t - time) < .00001f));
                var label = guide.Q<Label>(); label.text = time.ToString("0.00");
                var width = EditorStyles.label.CalcSize(new GUIContent(label.text)).x + 5;
                label.style.width = width; label.style.left = -width * .5f;
                guide.MarkDirtyRepaint();
            }
        }
        public void SyncRuler() { ruler.style.left = Left; ruler.MarkDirtyRepaint(); }
        public List<IDirectable> Rows()
        {
            return TimelineGeometry.VisibleRows(Asset);
        }
        public Rect ClipRect(Clip clip, int row)
        {
            return Transform.ClipRect(clip, row * 28 - owner.ScrollY, 26, 8, 4);
        }
        public Clip Hit(Vector2 p)
        {
            if (p.x < Left) return null;
            var rows = Rows(); var row = TimelineGeometry.RowAt(p.y, owner.ScrollY, 28);
            return row >= 0 && row < rows.Count && rows[row] is Track track ? track.Clips.LastOrDefault(c => ClipRect(c, row).Contains(p)) : null;
        }
        private float Snap(float time)
        {
            var step = Prefs.timeStepMode == Prefs.TimeStepMode.Frames ? 1f / Mathf.Max(1, Prefs.FrameRate) : Prefs.SnapInterval;
            return Mathf.Max(0, Prefs.MagnetSnapping && step > 0 ? Mathf.Round(time / step) * step : time);
        }
        private GestureMode ResizeMode(Clip clip, Vector2 position)
        {
            if (clip == null || clip.IsLocked || !clip.CanScale()) return GestureMode.None;
            var row = TimelineGeometry.RowAt(position.y, owner.ScrollY, 28);
            var rect = ClipRect(clip, row);
            var edge = TimelineGeometry.ResizeEdge(rect, position, Styles.ClipScaleRectWidth);
            return edge < 0 ? GestureMode.ResizeLeft : edge > 0 ? GestureMode.ResizeRight : GestureMode.None;
        }
        private void UpdateResizeCursor()
        {
            var resizing = mode == GestureMode.ResizeLeft || mode == GestureMode.ResizeRight;
            if (mode == GestureMode.None && hasHoverPosition && contentRect.Contains(hoverPosition))
                resizing = ResizeMode(Hit(hoverPosition), hoverPosition) != GestureMode.None;
            EnableInClassList("json-resize-horizontal", resizing);
        }
        private void Down(PointerDownEvent e)
        {
            if (e.target is VisualElement source && (welcome.Contains(source) || addGroup.Contains(source) || source is Button || source.parent is Button)) return;
            if (Asset == null || e.button == 1) return;
            if (owner.Context.Session.IsEditing && edit?.IsActive != true) return;
            Focus(); down = last = e.localPosition;
            var rows = Rows(); var row = TimelineGeometry.RowAt(down.y, owner.ScrollY, 28);
            hit = Hit(down);
            if (e.button == 2) mode = GestureMode.Pan;
            else if (Mathf.Abs(down.x - (Left - 7)) < 5) mode = GestureMode.Splitter;
            else if (down.x < Left)
            {
                if (row >= 0 && row < rows.Count)
                {
                    if (rows[row] is Group group && down.x < 20) { group.IsCollapsed = !group.IsCollapsed; owner.SyncView(); MarkDirtyRepaint(); }
                    else
                    {
                        owner.Select(rows[row]);
                        if (rows[row] is Track track && !track.IsLocked)
                        { movingTrack = track; mode = GestureMode.Track; pointer = e.pointerId; this.CapturePointer(pointer); e.StopPropagation(); }
                    }
                }
                return;
            }
            else if (hit != null)
            {
                if (!App.IsSelect(hit)) owner.Select(owner.Context.Selection.ClickCandidate(hit, e.ctrlKey, true));
                if (hit.IsLocked) return;
                mode = ResizeMode(hit, down); if (mode == GestureMode.None) mode = GestureMode.Move;
                original.Clear(); foreach (var clip in App.SelectItems.OfType<Clip>().Where(c => !c.IsLocked)) original[clip] = new Vector2(clip.StartTime, clip.Length);
                snapTimes = TimelineCandidates.References(Asset.groups.SelectMany(g => g.Tracks).SelectMany(t => t.Clips),
                    original.Keys, Asset.Length, AssetPlayer.Inst.CurrentTime, false);
                dragMoved = false;
                edit = owner.Context.Session.BeginEdit(this);
                if (edit == null) { mode = GestureMode.None; return; }
            }
            else { mode = GestureMode.Marquee; if (!e.ctrlKey) owner.Select(); }
            pointer = e.pointerId; UpdateResizeCursor(); this.CapturePointer(pointer); e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            hoverPosition = this.WorldToLocal(e.position); hasHoverPosition = true; UpdateResizeCursor();
            if (pointer != e.pointerId || mode == GestureMode.None || mode == GestureMode.Seek || Asset == null) return;
            if (EditsClips(mode) && edit?.IsActive != true) { Cancel(); return; }
            var p = (Vector2)e.localPosition;
            if (mode == GestureMode.Pan)
            {
                var range = TimelineNavigation.Pan(Asset.ViewTimeMin, Asset.ViewTimeMax, p.x - last.x, owner.rootVisualElement.contentRect.width - Left, true);
                Asset.ViewTimeMax = range.y; Asset.ViewTimeMin = range.x;
            }
            else if (mode == GestureMode.Splitter) { owner.LeftWidth = Mathf.Clamp(owner.LeftWidth + p.x - last.x, 120, Mathf.Max(120, contentRect.width - 120)); Styles.TimelineLeftWidth = owner.LeftWidth; }
            else if (mode == GestureMode.Marquee) marquee = Rect.MinMaxRect(Mathf.Max(Left, Mathf.Min(down.x, p.x)), Mathf.Min(down.y, p.y), Mathf.Max(down.x, p.x), Mathf.Max(down.y, p.y));
            else if (hit != null)
            {
                dragMoved |= p != down;
                var delta = (p.x - down.x) / Scale;
                if (mode == GestureMode.Move)
                {
                    delta = Magnet(Snap(original[hit].x + delta), original[hit].y, e.shiftKey) - original[hit].x;
                    delta = TimelineCandidates.ClampMoveDelta(delta, original.Values.Select(v => v.x));
                    foreach (var pair in original) pair.Key.StartTime = pair.Value.x + delta;
                }
                else if (mode == GestureMode.ResizeLeft)
                {
                    var candidate = TimelineCandidates.Resize(original[hit], true, Magnet(Snap(original[hit].x + delta), 0, e.shiftKey), .01f, 0, float.PositiveInfinity);
                    hit.StartTime = candidate.x; hit.Length = candidate.y;
                }
                else if (mode == GestureMode.ResizeRight) hit.Length = TimelineCandidates.Resize(original[hit], false, Magnet(Snap(original[hit].x + original[hit].y + delta), 0, e.shiftKey), .01f, 0, float.PositiveInfinity).y;
            }
            last = p; owner.SyncView(); MarkDirtyRepaint(); ruler.MarkDirtyRepaint(); e.StopPropagation();
        }
        private void MoveSafely(PointerMoveEvent e)
        {
            try { Move(e); }
            catch { Cancel(); throw; }
        }
        private void Up(PointerUpEvent e)
        {
            if (pointer != e.pointerId) return;
            if (e.button != (mode == GestureMode.Pan ? 2 : 0)) { e.PreventDefault(); e.StopImmediatePropagation(); return; }
            if (mode == GestureMode.Track && movingTrack != null)
            {
                var rows = Rows(); var row = Mathf.FloorToInt((this.WorldToLocal(e.position).y + owner.ScrollY) / 28);
                if (row >= 0 && row < rows.Count)
                {
                    var destination = rows[row]; var track = movingTrack;
                    if (destination is Group group) owner.Run(() => JsonEditCommands.MoveTrack(track, group, 0));
                    else if (destination is Track next && next != track) owner.Run(() => JsonEditCommands.MoveTrack(track, next.Group, next.Group.Tracks.IndexOf(next) + 1));
                }
            }
            if (mode == GestureMode.Marquee && marquee.HasValue)
            {
                var rows = Rows(); owner.Select(rows.SelectMany((r, i) => r is Track t ? t.Clips.Where(c => TimelineGeometry.Overlaps(ClipRect(c, i), marquee.Value)) : Enumerable.Empty<Clip>()).Cast<IDirectable>().ToArray());
            }
            var captured = pointer; var wasEdit = EditsClips(mode); pointer = -1; mode = GestureMode.None; marquee = null; movingTrack = null;
            dragMoved = false; SyncDragGuides();
            hasHoverPosition = false; UpdateResizeCursor();
            var token = edit; edit = null;
            if (wasEdit) owner.Run(() => token?.Commit("Drag timeline items"));
            if (this.HasPointerCapture(captured)) this.ReleasePointer(captured); MarkDirtyRepaint();
        }
        public void Cancel()
        {
            hasHoverPosition = false; RemoveFromClassList("json-resize-horizontal");
            dragMoved = false; SyncDragGuides();
            var token = edit; edit = null;
            if (pointer < 0) { token?.Cancel(); return; }
            var captured = pointer; var previousMode = mode; pointer = -1; mode = GestureMode.None; marquee = null; movingTrack = null;
            if (EditsClips(previousMode)) token?.Cancel();
            if (previousMode == GestureMode.Seek) owner.Seek(seekBefore);
            if (this.HasPointerCapture(captured)) this.ReleasePointer(captured); MarkDirtyRepaint();
        }
        private void Key(KeyDownEvent e)
        {
            if (TextInputTarget(e.target)) return;
            if (e.keyCode == KeyCode.Escape) { Cancel(); e.StopPropagation(); }
            else if (e.keyCode == KeyCode.Delete) owner.Run(JsonEditCommands.DeleteSelection);
            else if (e.ctrlKey && e.keyCode == KeyCode.C) owner.Run(() => JsonEditCommands.Copy(App.FistSelect));
            else if (e.ctrlKey && e.keyCode == KeyCode.X) owner.Run(() => JsonEditCommands.Copy(App.FistSelect, true));
            else if (e.ctrlKey && e.keyCode == KeyCode.D) owner.Run(() => JsonEditCommands.Duplicate(App.FistSelect));
            else if (e.ctrlKey && e.keyCode == KeyCode.V) owner.Run(() => JsonEditCommands.Paste(App.FistSelect is Clip c ? c.Parent : App.FistSelect, AssetPlayer.Inst.CurrentTime));
            else if (e.ctrlKey && e.keyCode == KeyCode.S) owner.Run(App.SaveAsset);
            else if (e.ctrlKey && e.keyCode == KeyCode.Z) { if (e.shiftKey) Undo.PerformRedo(); else Undo.PerformUndo(); e.StopPropagation(); }
        }
        private float Magnet(float start, float length, bool bypass)
        {
            if (!Prefs.MagnetSnapping || bypass) return start;
            return TimelineCandidates.Magnet(start, length, snapTimes, Asset.ViewTime * .01f, false);
        }
        private void Wheel(WheelEvent e)
        {
            if (Asset == null) return; Cancel();
            if (e.localMousePosition.x < Left) owner.ScrollY = Mathf.Max(0, owner.ScrollY + e.delta.y * 20);
            else Asset.ViewTimeMax = TimelineNavigation.ZoomMaximum(Asset.ViewTimeMin, Asset.ViewTimeMax, e.delta.y, owner.rootVisualElement.contentRect.width, Prefs.scrollWheelZooms);
            owner.SyncView(); MarkDirtyRepaint(); ruler.MarkDirtyRepaint(); e.StopPropagation();
        }
        public void ShowGroupMenu()
        {
            if (Asset == null) return;
            var menu = new GenericMenu();
            foreach (var info in EditorTools.GetTypeMetaDerivedFrom(typeof(Group)).Where(t => !t.type.IsAbstract && (t.attachableTypes == null || t.attachableTypes.Contains(Asset.GetType()))))
            {
                var type = info.type; var command = JsonEditCommands.Creation(Asset, type);
                if (command.CanExecute(out var reason)) menu.AddItem(new GUIContent(info.name), false, () => owner.Run(() => JsonEditCommands.AddGroup(type)));
                else menu.AddDisabledItem(new GUIContent(info.name, reason));
            }
            menu.ShowAsContext();
        }
        private void RebuildHeaders()
        {
            rowHeaders.Clear(); var rows = Rows();
            for (var i = 0; i < rows.Count; i++)
            {
                var item = rows[i]; var row = new VisualElement { pickingMode = PickingMode.Ignore }; row.style.position = Position.Absolute;
                row.style.left = 0; row.style.top = i * 28; row.style.height = 26; rowHeaders.Add(row);
                void Icon(Texture2D texture, float left, Action click = null, float opacity = 1)
                {
                    VisualElement element = click == null ? (VisualElement)new VisualElement { pickingMode = PickingMode.Ignore } : new Button(() => owner.Run(click));
                    element.style.position = Position.Absolute;
                    if (left < 0) element.style.right = -left; else element.style.left = left;
                    element.style.top = 5; element.style.width = element.style.height = 16;
                    element.style.paddingLeft = element.style.paddingRight = element.style.paddingTop = element.style.paddingBottom = 0;
                    element.style.marginLeft = element.style.marginRight = element.style.marginTop = element.style.marginBottom = 0;
                    element.style.borderLeftWidth = element.style.borderRightWidth = element.style.borderTopWidth = element.style.borderBottomWidth = 0;
                    element.style.backgroundColor = Color.clear; element.style.opacity = opacity;
                    var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }; image.style.flexGrow = 1; element.Add(image); row.Add(element);
                }
                if (item is Group group) Icon(group.IsCollapsed ? Styles.CollapsedIcon : Styles.ExpandedIcon, 4, () => { group.IsCollapsed = !group.IsCollapsed; RefreshVisualState(); owner.SyncView(); }, .5f);
                else if (item is Track track) Icon(track.GetIcon(), 28);
                if (item is Group) Icon(Styles.MenuIcon, -.001f, () => ShowHeaderMenu(item), .5f);
                var right = item is Group ? 34 : 18;
                if (item.IsLocked) { Icon(Styles.LockIcon, -(right - 16), () => JsonEditCommands.ToggleLock(item), .5f); right += 18; }
                if (!item.IsActive) Icon(Styles.EyeIcon, -(right - 16), () => JsonEditCommands.ToggleActive(item), .5f);
            }
            SyncAddGroup();
        }
        private void ShowHeaderMenu(IDirectable item)
        {
            var menu = new GenericMenu();
            void Entry(string text, Action action, bool enabled = true, bool check = false)
            { if (enabled) menu.AddItem(new GUIContent(text), check, () => owner.Run(action)); else menu.AddDisabledItem(new GUIContent(text)); }
            if (item is Group group)
            {
                foreach (var info in EditorTools.GetTypeMetaDerivedFrom(typeof(Track)).Where(t => !t.type.IsAbstract && t.attachableTypes?.Contains(group.GetType()) == true))
                { var type = info.type; Entry(Lan.MenuAddTrack + "/" + (string.IsNullOrEmpty(info.category) ? "" : info.category + "/") + info.name, () => JsonEditCommands.AddTrack(group, type), JsonEditCommands.Creation(group, type).CanExecute(out _)); }
                menu.AddSeparator("");
                if (App.CopyAsset is Track) Entry(Lan.MenuPasteTrack, () => JsonEditCommands.Paste(group), JsonEditCommands.CanPaste(group));
                Entry(Lan.GroupDisable, () => JsonEditCommands.ToggleActive(group), !group.IsLocked, !group.IsActive);
                Entry(Lan.GroupLocked, () => JsonEditCommands.ToggleLock(group), item.Parent?.IsLocked != true, group.IsLocked);
                Entry(Lan.Rename, () => RenameItemWindow.Show(group), !group.IsLocked); menu.AddSeparator("");
                Entry(Lan.GroupDelete, () => { if (EditorUtility.DisplayDialog(Lan.GroupDelete, Lan.GroupDeleteTips, Lan.TipsConfirm, Lan.TipsCancel)) JsonEditCommands.Delete(group); }, !group.IsLocked);
            }
            else if (item is Track track)
            {
                Entry(Lan.TrackDisable, () => JsonEditCommands.ToggleActive(track), !track.IsLocked, !track.IsActive);
                Entry(Lan.TrackLocked, () => JsonEditCommands.ToggleLock(track), track.Parent?.IsLocked != true, track.IsLocked); menu.AddSeparator("");
                Entry(Lan.TrackCopy, () => JsonEditCommands.Copy(track));
                Entry(Lan.TrackReplica, () => JsonEditCommands.Duplicate(track), !track.IsLocked && track.GetType().RTGetAttribute<UniqueAttribute>(true) == null); menu.AddSeparator("");
                Entry(Lan.TrackDelete, () => { if (EditorUtility.DisplayDialog(Lan.TrackDelete, Lan.TrackDeleteTips, Lan.TipsConfirm, Lan.TipsCancel)) JsonEditCommands.Delete(track); }, !track.IsLocked);
            }
            menu.ShowAsContext();
        }
        private void Menu(ContextualMenuPopulateEvent e)
        {
            if (mode != GestureMode.None || edit?.IsActive == true) { e.PreventDefault(); e.StopImmediatePropagation(); return; }
            if (Asset == null) return;
            var p = this.WorldToLocal(e.mousePosition); var rows = Rows(); var row = TimelineGeometry.RowAt(p.y, owner.ScrollY, 28);
            var clip = Hit(p); IDirectable item = clip ?? (row >= 0 && row < rows.Count ? rows[row] : null);
            if (item != null && p.x < Left) { e.menu.ClearItems(); ShowHeaderMenu(item); return; }
            if (item == null) return;
            void Action(string name, System.Action action, bool enabled = true) => e.menu.AppendAction(name, _ => owner.Run(action), enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            if (clip != null)
            {
                owner.Select(clip);
                if (clip.IsLocked) { Action("片段已锁定，请先解锁轨道", () => { }, false); return; }
                Action(Lan.ClipCopy, () => JsonEditCommands.Copy(clip)); Action(Lan.ClipCut, () => JsonEditCommands.Copy(clip, true));
                if (clip is ISubClipContainable source && source.SubClipLength > 0)
                {
                    e.menu.AppendSeparator();
                    Action(Lan.MatchPreviousLoop, () => App.Session.Edit("Match previous loop", clip.TryMatchPreviousSubClipLoop));
                    Action(Lan.MatchClipLength, () => App.Session.Edit("Match source", clip.TryMatchSubClipLength));
                    Action(Lan.MatchNextLoop, () => App.Session.Edit("Match next loop", clip.TryMatchNexSubClipLoop));
                }
                e.menu.AppendSeparator(); Action(Lan.ClipDelete, () => JsonEditCommands.Delete(clip)); return;
            }
            if (item is Track track && !track.IsLocked)
            {
                var category = track.Clips.FirstOrDefault()?.GetType().RTGetAttribute<CategoryAttribute>(true)?.category;
                foreach (var info in EditorTools.GetTypeMetaDerivedFrom(typeof(Clip)).Where(t => !t.type.IsAbstract && (t.attachableTypes == null || t.attachableTypes.Contains(track.GetType())) && (category == null || category == t.category)))
                { var type = info.type; Action((string.IsNullOrEmpty(info.category) ? "" : info.category + "/") + info.name, () => JsonEditCommands.AddClip(track, type, Snap(TimeAt(p.x))), JsonEditCommands.Creation(track, type, Snap(TimeAt(p.x))).CanExecute(out _)); }
                if (App.CopyAsset is Clip copied && JsonEditCommands.CanPaste(track))
                { e.menu.AppendSeparator(); Action(string.Format(Lan.ClipPaste, copied.GetType().Name), () => JsonEditCommands.Paste(track, Snap(TimeAt(p.x)))); }
            }
        }
        private void Draw(MeshGenerationContext c)
        {
            var p = c.painter2D; Fill(p, contentRect, new Color(.21f, .21f, .21f));
            if (Asset == null) return;
            var rows = Rows();
            for (var i = 0; i < rows.Count; i++)
            {
                var y = i * 28 - owner.ScrollY; if (y + 28 < 0 || y > contentRect.height) continue;
                var item = rows[i];
                Fill(p, new Rect(0, y, contentRect.width, 26), App.IsSelect(item) ? new Color(.2f, .32f, .48f) : new Color(.18f, .18f, .18f));
                if (item is Track track)
                {
                    foreach (var clip in track.Clips.OrderBy(clip => clip.StartTime))
                    {
                        var rect = ClipRect(clip, i); var visible = rect; visible.xMin = Mathf.Max(Left, rect.xMin); visible.xMax = Mathf.Min(contentRect.width, rect.xMax);
                        if (visible.width <= 0) continue;
                        if (clip.Length == 0)
                        {
                            Texture(c, new Rect(rect.center.x - Styles.SignalIcon.width * .5f, y, Styles.SignalIcon.width, Styles.SignalIcon.height), Styles.SignalIcon,
                                App.IsSelect(clip) ? new Color(153 / 255f, 242 / 255f, 1) : Styles.ClipBackColor);
                            continue;
                        }
                        Fill(p, visible, Styles.ClipBackColor);
                        Fill(p, new Rect(visible.x, visible.yMax - 4, visible.width, 4), clip.GetColor());
                        Blend(p, rect.xMin, X(clip.StartTime + Mathf.Min(clip.Length, clip.BlendIn)), rect, false);
                        var next = clip.GetNextSibling();
                        if (next == null || next.StartTime >= clip.EndTime) Blend(p, X(clip.EndTime - Mathf.Min(clip.Length, clip.BlendOut)), rect.xMax, rect, true);
                        var previous = clip.GetPreviousSibling();
                        if (previous != null && previous.EndTime > clip.StartTime) Line(p, new Vector2(X(clip.StartTime + clip.BlendIn), rect.y), new Vector2(X(clip.StartTime + clip.BlendIn), rect.yMax), new Color(0, 0, 0, .4f));
                        if (App.IsSelect(clip))
                        {
                            Fill(p, new Rect(visible.x, y, visible.width, 2), Styles.ClipSelectColor);
                            Fill(p, new Rect(visible.x, y + 24, visible.width, 2), Styles.ClipSelectColor);
                            if (rect.xMin >= Left) Fill(p, new Rect(rect.xMin, y, 2, 26), Styles.ClipSelectColor);
                            if (rect.xMax <= contentRect.width) Fill(p, new Rect(rect.xMax - 2, y, 2, 26), Styles.ClipSelectColor);
                        }
                        if (!clip.IsValid) Fill(p, visible, new Color(1, 0, 0, .2f));
                    }
                    Fill(p, new Rect(22, y + 3, 4, 20), track.GetColor());
                }
                Text(c, item.Name, item is Group ? 20 : 48, y + 5, owner.LeftWidth - (item is Group ? 50 : 78), item is Group ? new Color(1, 1, 1, .5f) : Color.white);
                if (item.IsLocked || !item.IsActive)
                {
                    var stamp = !item.IsActive && item.IsLocked ? "禁用 & 锁定" : item.IsLocked ? "锁定" : "禁用";
                    Fill(p, new Rect(0, y, contentRect.width, 26), new Color(0, 0, 0, item.IsActive ? .15f : .2f));
                    for (var x = 0f; x < contentRect.width; x += item.IsActive ? 20 : 5)
                        Line(p, new Vector2(x, y + 26), new Vector2(x + 26, y), new Color(0, 0, 0, .08f));
                    var size = EditorStyles.label.CalcSize(new GUIContent(stamp)); c.DrawText(stamp, new Vector2(Left + (contentRect.width - Left - size.x) * .5f, y + 4), 12, Color.white);
                }
            }
            Line(p, new Vector2(Left - 7, 0), new Vector2(Left - 7, contentRect.height), Color.gray);
            if (App.IsPlay && X(AssetPlayer.Inst.CurrentTime) >= Left) Line(p, new Vector2(X(AssetPlayer.Inst.CurrentTime), 0), new Vector2(X(AssetPlayer.Inst.CurrentTime), contentRect.height), Color.white);
            if (X(Asset.Length) >= Left) Line(p, new Vector2(X(Asset.Length), 0), new Vector2(X(Asset.Length), contentRect.height), Styles.EndPointerColor);
            if (App.IsRange) foreach (var time in new[] { Asset.RangeMin, Asset.RangeMax }) if (X(time) >= Left) Line(p, new Vector2(X(time), 0), new Vector2(X(time), contentRect.height), Color.white);
            if (marquee.HasValue) Fill(p, marquee.Value, new Color(.3f, .6f, 1, .25f));
        }
        private float TickStep()
        {
            foreach (var value in TickModulos) if (value * Scale > 50) return value;
            return 1000000;
        }
        private static readonly float[] TickModulos = { .01f, .1f, .5f, 1, 5, 10, 50, 100, 500, 1000, 5000, 10000, 50000, 100000, 250000, 500000 };
        private void DrawRuler(MeshGenerationContext c)
        {
            var p = c.painter2D; Fill(p, ruler.contentRect, new Color(.22f, .22f, .22f)); if (Asset == null) return;
            var step = TickStep();
            var index = Array.IndexOf(TickModulos, step); var minor = index > 0 ? TickModulos[index - 1] : .01f;
            Fill(p, new Rect(0, 16, Mathf.Clamp(X(Asset.Length) - Left, 0, ruler.contentRect.width), 4), Styles.TimeStepRectColor);
            if (minor * Scale > 6) for (var t = Mathf.Ceil(Asset.ViewTimeMin / minor) * minor; t <= Asset.ViewTimeMax; t += minor)
            { var x = X(t) - Left; Line(p, new Vector2(x, 16), new Vector2(x, 20), Color.white); }
            for (var t = Mathf.Ceil(Asset.ViewTimeMin / step) * step; t <= Asset.ViewTimeMax; t += step)
            {
                var x = X(t) - Left; if (x < 0) continue;
                var label = (Mathf.Round(t * 10) / 10).ToString("0.00");
                c.DrawText(label, new Vector2(x, 0), 12, Color.white); Line(p, new Vector2(x, 8), new Vector2(x, 20), Color.white);
            }
            var end = X(Asset.Length) - Left; Fill(p, new Rect(end - 2, 0, 5, 20), Styles.EndPointerColor);
            if (App.IsPlay) Texture(c, new Rect(X(AssetPlayer.Inst.CurrentTime) - Left - 5, 0, 11, 20), Styles.TimelineTimeCursorIcon, Color.white);
            if (App.IsRange)
            {
                Texture(c, new Rect(X(Asset.RangeMin) - Left - Styles.TimelineStartPlaybackIcon.width, 0, Styles.TimelineStartPlaybackIcon.width, Styles.TimelineStartPlaybackIcon.height), Styles.TimelineStartPlaybackIcon, Color.white);
                Texture(c, new Rect(X(Asset.RangeMax) - Left, 0, Styles.TimelineEndPlaybackIcon.width, Styles.TimelineEndPlaybackIcon.height), Styles.TimelineEndPlaybackIcon, Color.white);
            }
        }
        private static void Text(MeshGenerationContext c, string text, float x, float y, float width, Color color)
        {
            text = text ?? ""; var count = Mathf.Max(1, Mathf.FloorToInt(width / 8)); if (text.Length > count) text = text.Substring(0, count) + "…";
            c.DrawText(text, new Vector2(x, y), 12, color);
        }
        private void Blend(Painter2D p, float start, float end, Rect rect, bool outgoing)
        {
            if (end <= start) return;
            var left = Mathf.Max(Left, start); var right = Mathf.Min(contentRect.width, end);
            if (right <= left) return;
            var a = Mathf.InverseLerp(start, end, left); var b = Mathf.InverseLerp(start, end, right);
            var ya = Mathf.Lerp(outgoing ? rect.yMin : rect.yMax, outgoing ? rect.yMax : rect.yMin, a);
            var yb = Mathf.Lerp(outgoing ? rect.yMin : rect.yMax, outgoing ? rect.yMax : rect.yMin, b);
            p.fillColor = Styles.ClipBlendColor; p.BeginPath(); p.MoveTo(new Vector2(left, rect.yMin));
            p.LineTo(new Vector2(right, rect.yMin)); p.LineTo(new Vector2(right, yb)); p.LineTo(new Vector2(left, ya)); p.ClosePath(); p.Fill();
            Line(p, new Vector2(left, ya), new Vector2(right, yb), new Color(0, 0, 0, .5f));
        }
        private static void Texture(MeshGenerationContext context, Rect rect, Texture texture, Color tint)
        {
            if (texture == null || rect.width <= 0 || rect.height <= 0) return;
            var mesh = context.Allocate(4, 6, texture); var uv = mesh.uvRegion;
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMin, uv.yMax) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMax, uv.yMax) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMax, uv.yMin) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = tint, uv = new Vector2(uv.xMin, uv.yMin) });
            foreach (ushort index in new ushort[] { 0, 1, 2, 2, 3, 0 }) mesh.SetNextIndex(index);
        }
        private static void Fill(Painter2D p, Rect rect, Color color)
        {
            p.fillColor = color; p.BeginPath(); p.MoveTo(new Vector2(rect.xMin, rect.yMin)); p.LineTo(new Vector2(rect.xMax, rect.yMin));
            p.LineTo(new Vector2(rect.xMax, rect.yMax)); p.LineTo(new Vector2(rect.xMin, rect.yMax)); p.ClosePath(); p.Fill();
        }
        private static void Line(Painter2D p, Vector2 a, Vector2 b, Color color)
        { p.strokeColor = color; p.lineWidth = 1; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
    }
}
