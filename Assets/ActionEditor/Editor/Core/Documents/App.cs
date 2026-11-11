using System;
using UnityEditor;
using UnityEngine;
using NBC.ActionEditor.Storage;

namespace NBC.ActionEditor
{
    public delegate void CallbackFunction();

    public delegate void OpenAssetFunction(Asset asset);

    public static class App
    {
        public static CallbackFunction OnInitialize;
        public static CallbackFunction OnDisable;
        public static OpenAssetFunction OnOpenAsset;
        
        public static ActionEditorContext Context { get; } = new ActionEditorContext(new JsonDocumentSession());
        public static ActionEditorViews Views { get; } = new ActionEditorViews();
        public static JsonDocumentSession Session => Context.Session;
        public static Asset AssetData => Context.Document;
        public static event Action DocumentChanged;

        static App()
        {
            Context.Changed += () =>
            {
                Refresh(); Views.NotifyChanged(); Repaint(); DocumentChanged?.Invoke();
            };
            Context.Playback.Played += () => OnPlay?.Invoke();
            Context.Playback.Stopped += () => OnStop?.Invoke();
        }

        public static TextAsset TextAsset
        {
            get => Context.Source;
            set
            {
                try
                {
                    Context.Open(value);
                    if (AssetData != null) OnOpenAsset?.Invoke(AssetData);
                }
                catch (System.IO.InvalidDataException e) { Debug.LogError("ActionEditor 打开失败，原文档已保留：" + e.Message); }
            }
        }

        public static long Frame;

        public static bool IsJsonDocumentPath(string path) => string.Equals(System.IO.Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

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

        public static IDirectable CopyAsset { get => Context.Clipboard.Item; set => Context.Clipboard.Item = value; }
        public static bool IsCut { get => Context.Clipboard.IsCut; set => Context.Clipboard.IsCut = value; }

        #endregion

        #region Select

        public static IDirectable[] SelectItems => Context.Selection.Items;
        public static int SelectCount => Context.Selection.Count;

        public static IDirectable FistSelect => Context.Selection.First;

        public static bool CanMultipleSelect { get => Context.Selection.CanMultipleSelect; set => Context.Selection.CanMultipleSelect = value; }

        public static void Select(params IDirectable[] objs) => Context.Selection.Set(objs, true);

        public static bool IsSelect(IDirectable directable)
        {
            return Context.Selection.Contains(directable);
        }

        public static void SelectWithoutInspector(params IDirectable[] items)
        {
            Context.Selection.Set(items, false); Repaint();
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


        public static event Action RepaintRequested;
        public static void Repaint()
        {
            Views.Repaint();
            RepaintRequested?.Invoke();
        }

        #endregion

        #region 播放相关

        public static CallbackFunction OnPlay;
        public static CallbackFunction OnStop;

        public static bool IsPlay => Context.Playback.IsPlaying;
        public static bool IsPause => Context.Playback.IsPaused;
        public static bool IsRange { get => Context.Playback.IsRange; set => Context.Playback.IsRange = value; }

        public static void Play(Action callback = null)
        {
            if (Application.isPlaying || AssetData == null)
            {
                return;
            }

            Context.Playback.Play(Time.realtimeSinceStartup);
        }

        public static void Pause(bool pause = true)
        {
            Context.Playback.Pause(pause);
        }

        public static void Stop()
        {
            Context.Playback.Stop();
        }

        public static void StepForward()
        {
            Context.Playback.StepForward(Prefs.SnapInterval);
        }

        public static void StepBackward()
        {
            Context.Playback.StepBackward(Prefs.SnapInterval);
        }


        private static void PlayerUpdate()
        {
            if (Context.Playback.Update(Time.realtimeSinceStartup, Time.timeScale)) Repaint();
        }

        #endregion
    }
}