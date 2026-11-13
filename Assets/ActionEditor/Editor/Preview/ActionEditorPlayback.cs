using System;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 播放状态拥有一次运行代次，业务回调停止后不得继续循环采样。
    public sealed class ActionEditorPlayback
    {
        public AssetPlayer Player { get; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsRange { get; set; }
        private float previousTime;
        private long generation;
        public event Action Played;
        public event Action Stopped;

        public ActionEditorPlayback(AssetPlayer player)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Player.Faulted += () => { IsPlaying = false; IsPaused = false; };
        }
        public void Play(float now)
        {
            if (Player.Asset == null) return;
            var version = ++generation;
            previousTime = now; IsPaused = false; IsPlaying = true;
            try { Played?.Invoke(); }
            catch { if (generation == version) Stop(false); throw; }
        }
        public void Pause(bool paused) => IsPaused = paused;
        public void Stop(bool notify = true)
        {
            generation++;
            Player.CurrentTime = 0;
            IsPlaying = false; IsPaused = false;
            try { if (notify) Stopped?.Invoke(); }
            finally { Player.Reset(); }
        }
        public void StepForward(float interval) => Player.Sample(Math.Abs(Player.CurrentTime - Player.Length) < .00001f ? 0 : Player.CurrentTime + interval);
        public void StepBackward(float interval) => Player.Sample(Player.CurrentTime == 0 ? Player.Length : Player.CurrentTime - interval);
        public bool Update(float now, float timeScale)
        {
            var delta = (now - previousTime) * timeScale;
            var version = generation;
            previousTime = now;
            var document = Player.Asset;
            if (document == null || !IsPlaying || IsPaused) return false;
            var min = IsRange ? document.RangeMin : 0;
            var max = IsRange ? Mathf.Min(document.RangeMax, document.Length) : document.Length;
            if (max <= min) { Stop(); return true; }
            if (Player.CurrentTime + delta >= max)
            {
                var next = min + Mathf.Repeat(Player.CurrentTime + delta - max, max - min);
                Player.Sample(max);
                if (generation != version || !IsPlaying || IsPaused) return true;
                Player.Reset();
                if (generation != version || !IsPlaying || IsPaused) return true;
                Player.Sample(next);
            }
            else Player.Sample(Player.CurrentTime + delta);
            return true;
        }
    }

}
