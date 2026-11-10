using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
using NBC.ActionEditor.Storage;

namespace NBC.ActionEditor
{
    public delegate void CallbackFunction();

    public delegate void OpenAssetFunction(Asset asset);

    public static class App
    {
        private static TextAsset _textAsset;

        public static CallbackFunction OnInitialize;
        public static CallbackFunction OnDisable;
        public static OpenAssetFunction OnOpenAsset;
        
        public static readonly JsonDocumentSession Session = new JsonDocumentSession();
        public static Asset AssetData => Session.Document;
        public static event Action DocumentChanged;

        static App()
        {
            Session.Changed += () =>
            {
                AssetPlayer.Inst.Reset();
                _selectList.RemoveAll(item => AssetData == null || !AssetData.directables.Contains(item));
                Refresh(); Repaint(); DocumentChanged?.Invoke();
            };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                SessionState.SetString("ActionEditor.JsonRecovery", Session.CaptureRecovery());
                AssetPlayer.Inst.Reset();
            };
            var recovery = SessionState.GetString("ActionEditor.JsonRecovery", "");
            if (!string.IsNullOrEmpty(recovery))
            {
                try
                {
                    Session.RestoreRecovery(recovery);
                    _textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Session.Path ?? "");
                }
                catch (Exception e) { Debug.LogError("ActionEditor 文档恢复失败，恢复快照仍保留：" + e.Message); }
            }
            EditorApplication.playModeStateChanged += _ => { Stop(); AssetPlayer.Inst.Reset(); };
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Stop();
        }

        public static TextAsset TextAsset
        {
            get => _textAsset;
            set
            {
                try
                {
                    Session.Open(value);
                    _textAsset = value;
                    IsPlay = false; IsPause = false;
                    AssetPlayer.Inst.CurrentTime = 0;
                    if (AssetData != null) OnOpenAsset?.Invoke(AssetData);
                }
                catch (System.IO.InvalidDataException e) { Debug.LogError("ActionEditor 打开失败，原文档已保留：" + e.Message); }
            }
        }

        public static EditorWindow Window;

        public static long Frame;

        public static float Width;

        public static void OnObjectPickerConfig(Object obj)
        {
            if (obj is TextAsset textAsset)
            {
                if (textAsset != TextAsset && !ConfirmDocumentSwitch()) return;
                TextAsset = textAsset;
            }
        }

        public static bool ConfirmDocumentSwitch()
        {
            if (!Session.IsDirty) return true;
            var choice = EditorUtility.DisplayDialogComplex("未保存的 JSON", "切换前是否保存当前文档？", "保存", "取消", "放弃");
            if (choice == 1) return false;
            if (choice == 0) { SaveAsset(); return !Session.IsDirty; }
            return true;
        }

        public static void SaveAsset()
        {
            Session.Save();
            _lastSaveTime = DateTime.Now;
            _lastSaveClock = EditorApplication.timeSinceStartup;
        }

        public static void OnGUIEnd()
        {
            if (Frame > NeedForceRefreshFrame)
            {
                NeedForceRefresh = false;
            }

            Frame++;
            if (Frame >= long.MaxValue)
            {
                Frame = 0;
            }
        }


        public static void OnUpdate()
        {
            TryAutoSave();
            PlayerUpdate();
        }

        #region AutoSave

        public static DateTime LastSaveTime => _lastSaveTime;

        private static DateTime _lastSaveTime = DateTime.Now;
        private static double _lastSaveClock = EditorApplication.timeSinceStartup;

        /// <summary>
        /// 尝试自动保存
        /// </summary>
        public static void TryAutoSave()
        {
            if (Session.IsEditing) return;
            if (AssetData == null || string.IsNullOrEmpty(Session.Path) || !Session.IsDirty) return;
            if (NBC.ActionEditor.Model.AutoSaveSchedule.IsDue(_lastSaveClock, EditorApplication.timeSinceStartup, Prefs.autoSaveSeconds))
            {
                _lastSaveClock = EditorApplication.timeSinceStartup;
                try { SaveAsset(); } catch (Exception e) { Debug.LogError("ActionEditor 自动保存失败，内存内容已保留：" + e.Message); }
            }
        }

        public static void AutoSave()
        {
            SaveAsset();
        }

        #endregion

        #region Copy&Cut

        public static IDirectable CopyAsset { get; set; }
        public static bool IsCut { get; set; }

        #endregion

        #region Select

        public static IDirectable[] SelectItems => _selectList.ToArray();
        public static int SelectCount => _selectList.Count;
        private static readonly List<IDirectable> _selectList = new List<IDirectable>();

        public static IDirectable FistSelect => _selectList.Count > 0 ? _selectList.First() : null;

        public static bool CanMultipleSelect { get; set; }

        [System.NonSerialized] private static InspectorPreviewAsset _currentInspectorPreviewAsset;

        public static InspectorPreviewAsset CurrentInspectorPreviewAsset
        {
            get
            {
                if (_currentInspectorPreviewAsset == null)
                {
                    _currentInspectorPreviewAsset = ScriptableObject.CreateInstance<InspectorPreviewAsset>();
                }

                return _currentInspectorPreviewAsset;
            }
        }

        public static void Select(params IDirectable[] objs)
        {
            var change = false;
            if (objs == null)
            {
                if (_selectList.Count > 0) change = true;
            }
            else
            {
                if (objs.Length != _selectList.Count) change = true;
                else
                {
                    var pickCount = 0;
                    foreach (var obj in objs)
                    {
                        if (_selectList.Contains(obj)) pickCount++;
                    }

                    if (pickCount != objs.Length)
                    {
                        change = true;
                    }
                }
            }

            if (!change) return;
            _selectList.Clear();
            if (objs != null)
            {
                foreach (var obj in objs)
                {
                    _selectList.Add(obj);
                }

                Selection.activeObject = CurrentInspectorPreviewAsset;
                EditorUtility.SetDirty(CurrentInspectorPreviewAsset);

                // DirectorUtility.selectedObject = FistSelect;
            }

            if (_selectList.Count == 1 && _selectList[0] is not Clip)
            {
                CanMultipleSelect = true;
            }
            else
            {
                CanMultipleSelect = false;
            }
        }

        public static bool IsSelect(IDirectable directable)
        {
            return _selectList.Contains(directable);
        }

        public static void SelectWithoutInspector(params IDirectable[] items)
        {
            _selectList.Clear(); if (items != null) _selectList.AddRange(items.Where(i => i != null));
            CanMultipleSelect = false; Repaint();
        }

        #endregion

        #region Refresh

        public static bool NeedForceRefresh { get; private set; }
        public static long NeedForceRefreshFrame { get; private set; }

        public static void Refresh()
        {
            NeedForceRefresh = true;
            NeedForceRefreshFrame = Frame;
        }


        public static void Repaint()
        {
            if (Window != null)
            {
                Window.Repaint();
            }
        }

        #endregion

        #region 播放相关

        public static CallbackFunction OnPlay;
        public static CallbackFunction OnStop;

        private static AssetPlayer _player => AssetPlayer.Inst;

        public static bool IsPlay { get; private set; }
        public static bool IsPause { get; private set; }

        public static bool IsRange { get; set; }

        private static float _editorPreviousTime;

        public static void Play(Action callback = null)
        {
            if (Application.isPlaying || AssetData == null)
            {
                return;
            }

            OnPlay?.Invoke();
            _editorPreviousTime = Time.realtimeSinceStartup;
            IsPause = false;
            IsPlay = true;
        }

        public static void Pause(bool pause = true)
        {
            IsPause = pause;
        }

        public static void Stop()
        {
            if (AssetData != null)
                _player.CurrentTime = 0;

            OnStop?.Invoke();
            _player.Reset();
            IsPlay = false;
            IsPause = false;
        }

        public static void StepForward()
        {
            if (Math.Abs(_player.CurrentTime - _player.Length) < 0.00001f)
            {
                _player.Sample(0);
                return;
            }

            _player.Sample(_player.CurrentTime + Prefs.SnapInterval);
        }

        public static void StepBackward()
        {
            if (_player.CurrentTime == 0)
            {
                _player.Sample(_player.Length);
                return;
            }

            _player.Sample(_player.CurrentTime - Prefs.SnapInterval);
        }


        private static void PlayerUpdate()
        {
            if (AssetData == null) return;
            var delta = (Time.realtimeSinceStartup - _editorPreviousTime) * Time.timeScale;

            _editorPreviousTime = Time.realtimeSinceStartup;

            if (!IsPlay) return;
            
            if(IsPause) return;

            var min = IsRange ? AssetData.RangeMin : 0;
            var max = IsRange ? Mathf.Min(AssetData.RangeMax, AssetData.Length) : AssetData.Length;
            if (max <= min) { Stop(); return; }
            if (_player.CurrentTime + delta >= max)
            {
                var next = min + Mathf.Repeat(_player.CurrentTime + delta - max, max - min);
                _player.Sample(max);
                _player.Reset();
                _player.Sample(next);
                Repaint();
                return;
            }

            _player.CurrentTime += delta; 
            _player.Sample();
            Repaint();
        }

        #endregion
    }
}