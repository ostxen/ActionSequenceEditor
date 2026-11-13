using NBC.ActionEditor.Domain;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Events;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public class TimelineMiddleView : ViewBase, IPointerDragHandler, IDragBeginHandler, IDragEndHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public ActionDocument asset => App.AssetData;


        #region Static

        private static readonly List<TimelineTrackItemView> _itemViews = new List<TimelineTrackItemView>();
        private static Rect viewport;
        public static TrackDropTarget TrackDrop(TimelineTrack track, Vector2 point, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace) => TimelineGeometry.TrackDrop(
            App.AssetData, track, point - viewport.position, workspace.ScrollY, viewport.width, viewport.height, Styles.LineHeight + Styles.Space, workspace);

        public static TimelineTrackItemView GetItem(Vector2 pos)
        {
            if (_itemViews != null)
            {
                foreach (var itemView in _itemViews)
                {
                    if (itemView.Position.Contains(pos))
                    {
                        return itemView;
                    }
                }
            }

            return default;
        }

        public static TimelineTrackItemView GetLastItem()
        {
            if (_itemViews != null && _itemViews.Count > 0)
            {
                return _itemViews.Last();
            }

            return null;
        }

        #endregion

        protected override void OnInit()
        {
            ResetViews();
        }

        public override void OnDraw()
        {
            viewport = Position;
            ClipDrawer.Reset();
            DrawList();
            DrawTrackDrop();
            DrawMultiple();
        }

        private void DrawTrackDrop()
        {
            var gesture = ((ActionEditorWindow)Window).Gesture;
            var drop = gesture.TrackDrop;
            if (gesture.DragType != ItemDragType.Track || !drop.IsValid) return;
            var color = gesture.MoveToItem != null ? new Color(.3f, .75f, 1) : Color.red;
            var y = Mathf.Clamp(drop.Y, 2, Position.height - 2);
            EditorGUI.DrawRect(new Rect(drop.Group == null ? 0 : 22, y, Workspace.LeftWidth - 22, 2), color);
            GUI.Label(new Rect(24, Mathf.Max(0, y - 20), Workspace.LeftWidth - 24, 20),
                gesture.MoveToItem == null ? "不能放置" : drop.Group == null ? "移到根级" : "加入组：" + drop.Group.Name);
        }
        

        #region List

        private void ResetViews()
        {
            void AddView(IDirectable data)
            {
                var view = Window.CreateView<TimelineTrackItemView>();
                view.SetData(data);
                _itemViews.Add(view);
            }

            foreach (var item in _itemViews) item.Release();
            _itemViews.Clear();
            if (asset == null) return;
            foreach (var item in TimelineGeometry.VisibleRows(asset, Workspace)) AddView(item);
        }


        private void DrawList()
        {
            if (_itemViews.Count < 1 || App.NeedForceRefresh)
            {
                ResetViews();
            }

            // 左侧列表部分
            GUILayout.BeginVertical();
            var scroll = EditorGUILayout.BeginScrollView(new Vector2(0, Workspace.ScrollY));
            Workspace.ScrollY = scroll.y;

            var maxHeight = _itemViews.Count * Styles.LineHeight + _itemViews.Count * Styles.Space;

            var width = Position.width;
            if (maxHeight > Position.height)
            {
                width -= GUI.skin.verticalScrollbar.fixedWidth;
            }

            for (int i = 0; i < _itemViews.Count; i++)
            {
                var y = Styles.LineHeight * i + i * Styles.Space;
                var itemRect = new Rect(0, y, width, Styles.LineHeight);
                var item = _itemViews[i];
                item.OnGUI(itemRect);
                GUILayout.Space(Styles.Space);
            }

            DrawAddGroupButton();
            EditorGUILayout.EndScrollView();

            GUILayout.EndVertical();
        }


        private void DrawAddGroupButton()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);
            var width = Workspace.LeftWidth - 30;
            if (GUILayout.Button(Lan.GroupAdd, GUILayout.Width(width), GUILayout.Height(24)))
            {
                List<TypeMetaInfo> list = new List<TypeMetaInfo>();

                var ts = ActionEditorTypeCatalog.DerivedFrom(typeof(TimelineGroup));
                foreach (var typeMetaInfo in ts)
                {
                    var info = typeMetaInfo;
                    if (info.type.IsAbstract) continue;
                    if (info.attachableTypes != null)
                    {
                        if (!info.attachableTypes.Contains(asset.GetType()))
                        {
                            continue;
                        }
                    }

                    list.Add(typeMetaInfo);
                }

                if (list.Count > 1)
                {
                    var menu = new GenericMenu();
                    foreach (var typeMetaInfo in list)
                    {
                        var info = typeMetaInfo;
                        var command = App.Context.Structure.Creation(asset, info.type);
                        if (command.CanExecute(out var reason)) menu.AddItem(new GUIContent(info.name), false,
                            () =>
                            {
                                App.Context.Structure.AddGroup(info.type);
                                App.Refresh();
                            });
                        else menu.AddDisabledItem(new GUIContent(info.name, reason));
                    }

                    menu.ShowAsContext();
                }
                else
                {
                    if (list.Count == 1) App.Context.Structure.AddGroup(list[0].type);
                    App.Refresh();
                }
            }

            GUILayout.EndHorizontal();
        }

        #endregion

        #region Events

        private bool _pointerDown;
        private Vector2 _pointerDownPos;
        public void CancelMarquee() { _pointerDown = false; _preMultipleResult = false; _multipleRect = default; }

        public void OnDragBegin(PointerEventData eventData)
        {
            ((ActionEditorWindow)Window).Gesture.OnBeginDrag(eventData, (Window as ActionEditorWindow)?.CurrentEdit);
        }

        public void OnPointerDrag(PointerEventData eventData)
        {
            ((ActionEditorWindow)Window).Gesture.OnDrag(eventData);
        }

        public void OnDragEnd(PointerEventData eventData)
        {
            ((ActionEditorWindow)Window).Gesture.OnEndDrag(eventData);
        }

        public void OnPointerDown(PointerEventData ev)
        {
            if (!ev.IsLeft()) return;
            _pointerDown = true;
            _pointerDownPos = ev.MousePosition;
        }

        public void OnPointerUp(PointerEventData ev)
        {
            _pointerDown = false;
        }

        #endregion

        #region 多选 multiple select

        private bool _preMultipleResult;
        private Rect _multipleRect;

        private void DrawMultiple()
        {
            var mousePosition = Event.current.mousePosition;
            var rect = new Rect();
            var bigEnough = false;
            var start = _pointerDownPos;
            if (start.x < Workspace.LeftWidth) return;
            if (_pointerDown && App.SelectCount <= 1)
            {
                if ((start - mousePosition).magnitude > 10)
                {
                    bigEnough = true;
                    rect.xMin = Mathf.Max(Mathf.Min(start.x, mousePosition.x), 0);
                    rect.xMax = Mathf.Min(Mathf.Max(start.x, mousePosition.x), Position.width);
                    rect.yMin = Mathf.Min(start.y, mousePosition.y);
                    rect.yMax = Mathf.Max(start.y, mousePosition.y);
                    if (rect.x < (Workspace.LeftWidth + Styles.SplitterWidth + Styles.RightGapWidth))
                    {
                        var offsetX = (Workspace.LeftWidth + Styles.SplitterWidth + Styles.RightGapWidth) - rect.x;
                        rect.x = (Workspace.LeftWidth + Styles.SplitterWidth + Styles.RightGapWidth);
                        rect.width -= offsetX;
                    }

                    _multipleRect = rect;
                }
            }

            if (bigEnough)
            {
                GUI.color = Color.white.WithAlpha(0.1f);
                GUI.DrawTexture(rect, Styles.WhiteTexture);
                Vector3 topLeft = new Vector3(rect.xMin, rect.yMin, 0);
                Vector3 topRight = new Vector3(rect.xMax, rect.yMin, 0);
                Vector3 bottomRight = new Vector3(rect.xMax, rect.yMax, 0);
                Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMax, 0);
                Handles.DrawAAPolyLine(2f, topLeft, topRight);
                Handles.DrawAAPolyLine(2f, topRight, bottomRight);
                Handles.DrawAAPolyLine(2f, bottomRight, bottomLeft);
                Handles.DrawAAPolyLine(2f, bottomLeft, topLeft);
                App.Repaint();
            }

            //check select clips
            if (!_pointerDown && _preMultipleResult)
            {
                var yOffset = Workspace.ScrollY;
                var relaRect = new Rect(_multipleRect.x - (Workspace.LeftWidth + Styles.SplitterWidth + Styles.RightGapWidth), _multipleRect.y + yOffset,
                    _multipleRect.width,
                    _multipleRect.height);
                var clips = ClipDrawer.GetClips(relaRect, Workspace);
                // ClipDrawer.GetClipByTrackPosition();
                App.Select(clips.ToArray());
            }


            _preMultipleResult = bigEnough;
        }

        #endregion
    }
}