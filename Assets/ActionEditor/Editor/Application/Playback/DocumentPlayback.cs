using System;
using NBC.ActionEditor.Application.Ports;

namespace NBC.ActionEditor.Application.Playback
{
    // 播放状态拥有一次运行代次，业务回调停止后不得继续循环采样。
    public sealed class DocumentPlayback : IDisposable
    {
        private readonly IDocumentSampler sampler;
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsRange { get; set; }
        private float previousTime;
        private long generation;
        private bool disposed;
        public event Action Played;
        public event Action Stopped;

        public DocumentPlayback(IDocumentSampler sampler)
        {
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            sampler.Faulted += OnFaulted;
        }
        private void OnFaulted() { generation++; IsPlaying = false; IsPaused = false; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; sampler.Faulted -= OnFaulted;
            try { Stop(false); }
            finally { Played = null; Stopped = null; }
        }
        public void Play(float now)
        {
            if (!sampler.HasDocument) return;
            var version = ++generation;
            previousTime = now; IsPaused = false; IsPlaying = true;
            try { Played?.Invoke(); }
            catch { if (generation == version) Stop(false); throw; }
        }
        public void Pause(bool paused) => IsPaused = paused;
        public void Stop(bool notify = true)
        {
            generation++;
            sampler.CurrentTime = 0;
            IsPlaying = false; IsPaused = false;
            try { if (notify) Stopped?.Invoke(); }
            finally { sampler.Reset(); }
        }
        public void StepForward(float interval) => sampler.Sample(Math.Abs(sampler.CurrentTime - sampler.Length) < .00001f ? 0 : sampler.CurrentTime + interval);
        public void StepBackward(float interval) => sampler.Sample(sampler.CurrentTime == 0 ? sampler.Length : sampler.CurrentTime - interval);
        public bool Update(float now, float timeScale)
        {
            var delta = (now - previousTime) * timeScale;
            var version = generation;
            previousTime = now;
            if (!sampler.HasDocument || !IsPlaying || IsPaused) return false;
            var min = IsRange ? sampler.RangeMin : 0;
            var max = IsRange ? Math.Min(sampler.RangeMax, sampler.Length) : sampler.Length;
            if (max <= min) { Stop(); return true; }
            if (sampler.CurrentTime + delta >= max)
            {
                var length = max - min;
                var elapsed = sampler.CurrentTime + delta - max;
                var remainder = elapsed - (float)Math.Floor(elapsed / length) * length;
                var next = min + Math.Max(0, Math.Min(remainder, length));
                sampler.Sample(max);
                if (generation != version || !IsPlaying || IsPaused) return true;
                sampler.Reset();
                if (generation != version || !IsPlaying || IsPaused) return true;
                sampler.Sample(next);
            }
            else sampler.Sample(sampler.CurrentTime + delta);
            return true;
        }
    }

}
