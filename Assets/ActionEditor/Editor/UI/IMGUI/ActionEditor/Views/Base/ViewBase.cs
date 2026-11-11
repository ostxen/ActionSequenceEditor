using System;
using NBC.ActionEditor.Events;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public abstract class ViewBase
    {
        protected EditorWindow Window;

        public Rect Position;

        private bool _visible;

        public bool Visible
        {
            get => _visible;
            set { _visible = value; }
        }

        public void Init(EditorWindow window)
        {
            Window = window;
            _visible = true;
            OnInit();
        }

        public void Update()
        {
            if (Position.width <= 0 && Position.height <= 0) return;
            OnUpdate();
        }


        protected virtual void OnInit()
        {
        }


        public virtual void OnGUI(Rect rect)
        {
            Position = rect;
            try
            {
                OnDraw();
                CheckPointerEvent();
            }
            catch (ExitGUIException) { throw; }
            catch (Exception e)
            {
                (Window as ActionEditorWindow)?.CancelOwnEdit();
                Debug.Log(e);
            }
        }
        
        public abstract void OnDraw();

        protected virtual void OnUpdate()
        {
        }

        #region Event Handler

        private bool _havePointerDown;
        private bool _isDragging;
        private bool _isPointerOver;
        private PointerEventData _eventData = new PointerEventData();
        private const float DragThreshold = 1f;
        private Vector2 _dragStartPos;
        private int _pointerButton;

        internal void ResetPointerState() { _havePointerDown = false; _isDragging = false; _isPointerOver = false; }

        protected void CheckPointerEvent()
        {
            CheckPointerEvent(Event.current);
        }

        protected void CheckPointerEvent(Event ev)
        {
            if (ev == null) return;
            _eventData.SetEvent(ev);
            // Debug.Log("CheckPointerEvent");
            var hasRect = Position.Contains(ev.mousePosition);

            // Children may consume the press for selection. Ancestors still need to remember
            // its origin so a later drag cannot start without a corresponding mouse-down.
            if (ev.type == EventType.Used && ev.rawType == EventType.MouseDown && hasRect)
            { _havePointerDown = true; _pointerButton = ev.button; _dragStartPos = ev.mousePosition; _isDragging = false; }

            _eventData.HasRect = hasRect;
            if (ev.type == EventType.MouseDown)
            {
                if (hasRect)
                {
                    _havePointerDown = true;
                    _pointerButton = ev.button;
                    _dragStartPos = ev.mousePosition;
                    _isDragging = false;
                    if (this is IPointerDownHandler pointerDownHandler)
                    {
                        pointerDownHandler.OnPointerDown(_eventData);
                    }
                }
                else
                {
                    _havePointerDown = false;
                }
            }
            else if (ev.rawType == EventType.MouseUp)
            {
                if (_havePointerDown && ev.button != _pointerButton) return;
                var wasDragging = _isDragging;
                if (_havePointerDown && this is IPointerUpHandler pointerUpHandler)
                {
                    pointerUpHandler.OnPointerUp(_eventData);
                }

                if (_isDragging && this is IDragEndHandler dragEndHandler)
                {
                    _isDragging = false;
                    dragEndHandler.OnDragEnd(_eventData);
                }

                if (hasRect)
                {
                    if (_havePointerDown && !wasDragging && this is IPointerClickHandler pointerClickHandler)
                    {
                        pointerClickHandler.OnPointerClick(_eventData);
                    }
                }
                else
                {
                    _havePointerDown = false;
                }
                _havePointerDown = false; _isDragging = false;
            }
            else if (ev.type == EventType.MouseDrag)
            {
                if (_havePointerDown && ev.button != _pointerButton) return;
                if (_havePointerDown)
                {
                    if (!_isDragging && Vector2.Distance(_dragStartPos, ev.mousePosition) > DragThreshold)
                    {
                        _isDragging = true; // 达到阈值，开始拖动 When the threshold is reached, drag begins
                        if (this is IDragBeginHandler dragBeginHandler)
                        {
                            dragBeginHandler.OnDragBegin(_eventData);
                        }
                    }

                    if (_isDragging && this is IPointerDragHandler dragHandler)
                    {
                        dragHandler.OnPointerDrag(_eventData);
                    }
                }
            }

            // if (ev.type == EventType.MouseMove)
            // {
            //     // if (_havePointerDown)
            //     {
            //         if (this is IPointerMoveHandler pointerMoveHandler)
            //         {
            //             pointerMoveHandler.OnPointerMove(_eventData);
            //         }
            //     }
            // }

            if (this is IPointerEnterHandler pointerEnterHandler)
            {
                if (hasRect && !_isPointerOver)
                {
                    _isPointerOver = true;
                    pointerEnterHandler.OnPointerEnter(_eventData);
                }
            }

            if (this is IPointerExitHandler pointerExitHandler)
            {
                if (!hasRect && _isPointerOver)
                {
                    _isPointerOver = false;
                    pointerExitHandler.OnPointerExit(_eventData);
                }
            }
        }

        #endregion
    }
}