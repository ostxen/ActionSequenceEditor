using System;

namespace NBC.ActionEditor.Model
{
    public enum EventKind { Point, Interval }
    public enum EventRole { Logic, Presentation }
    public enum OverlapPolicy { Allow, Reject }
    public enum PlaybackEndReason { Completed, Stopped, Cancelled, Faulted }
    public enum BoundaryPhase { Exit, Point, Enter }

    public readonly struct BoundaryKey : IComparable<BoundaryKey>
    {
        public readonly double Time;
        public readonly long Cycle;
        public readonly BoundaryPhase Phase;
        public readonly int GroupOrder;
        public readonly int TrackOrder;
        public readonly int EventOrder;
        public readonly string EventId;

        public BoundaryKey(double time, long cycle, BoundaryPhase phase, int groupOrder, int trackOrder, int eventOrder, string eventId)
        {
            TimelineTime.RequireNonNegative(time, nameof(time));
            if (cycle < 0) throw new ArgumentOutOfRangeException(nameof(cycle));
            if (!Enum.IsDefined(typeof(BoundaryPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (!StableId.IsValid(eventId)) throw new ArgumentException("Invalid persistent ID.", nameof(eventId));
            Time = time;
            Cycle = cycle;
            Phase = phase;
            GroupOrder = groupOrder;
            TrackOrder = trackOrder;
            EventOrder = eventOrder;
            EventId = eventId;
        }

        public int CompareTo(BoundaryKey other)
        {
            var result = Time.CompareTo(other.Time);
            if (result != 0) return result;
            result = Cycle.CompareTo(other.Cycle);
            if (result != 0) return result;
            result = Phase.CompareTo(other.Phase);
            if (result != 0) return result;
            result = GroupOrder.CompareTo(other.GroupOrder);
            if (result != 0) return result;
            result = TrackOrder.CompareTo(other.TrackOrder);
            if (result != 0) return result;
            result = EventOrder.CompareTo(other.EventOrder);
            return result != 0 ? result : string.CompareOrdinal(EventId, other.EventId);
        }
    }

    public static class TimelineTime
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static void RequireNonNegative(double value, string name)
        {
            if (!IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        }

        public static double FromFrames(long frames, double frameRate)
        {
            RequireRate(frameRate);
            if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
            return frames / frameRate;
        }

        public static long ToFrames(double seconds, double frameRate)
        {
            RequireNonNegative(seconds, nameof(seconds));
            RequireRate(frameRate);
            return checked((long)Math.Round(seconds * frameRate, MidpointRounding.AwayFromZero));
        }

        // The first advance includes zero. Every later advance uses (previous, current].
        public static bool CrossesPoint(double point, double previous, double current, bool initial = false)
        {
            RequireNonNegative(point, nameof(point));
            RequireNonNegative(previous, nameof(previous));
            RequireNonNegative(current, nameof(current));
            if (current < previous) throw new ArgumentException("Backward seeking is not forward execution.");
            if (initial && previous != 0) throw new ArgumentException("Initial advance must start at zero.");
            return point <= current && (point > previous || (initial && point == 0));
        }

        public static bool Contains(double start, double duration, double time)
        {
            RequireNonNegative(start, nameof(start));
            RequireNonNegative(duration, nameof(duration));
            RequireNonNegative(time, nameof(time));
            return duration > 0 && time >= start && time < start + duration;
        }

        public static bool Overlaps(double startA, double durationA, double startB, double durationB)
        {
            RequireNonNegative(startA, nameof(startA));
            RequireNonNegative(startB, nameof(startB));
            RequireNonNegative(durationA, nameof(durationA));
            RequireNonNegative(durationB, nameof(durationB));
            return durationA > 0 && durationB > 0 && startA < startB + durationB && startB < startA + durationA;
        }

        // Count occurrences at cycle*length+point, retaining endpoint and next-cycle zero as distinct identities.
        public static long CountLoopOccurrences(double point, double length, double previous, double current, bool initial = false)
        {
            RequireNonNegative(point, nameof(point));
            RequireNonNegative(previous, nameof(previous));
            RequireNonNegative(current, nameof(current));
            if (!IsFinite(length) || length <= 0 || point > length) throw new ArgumentOutOfRangeException(nameof(length));
            if (current < previous || (initial && previous != 0)) throw new ArgumentException("Invalid forward range.");
            var last = Math.Floor((current - point) / length);
            var first = Math.Max(0, Math.Floor((previous - point) / length) + 1);
            if (initial && point == 0) first = 0;
            return last < first ? 0 : checked((long)(last - first + 1));
        }

        private static void RequireRate(double rate)
        {
            if (!IsFinite(rate) || rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
        }
    }

    public static class StableId
    {
        public static string New() => Guid.NewGuid().ToString("N");

        public static bool IsValid(string value)
        {
            return value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out var id)
                && id != Guid.Empty && id.ToString("N") == value;
        }
    }
}
