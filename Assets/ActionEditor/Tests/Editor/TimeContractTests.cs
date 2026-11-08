using System;
using NBC.ActionEditor.Model;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests
{
    public class TimeContractTests
    {
        [TestCase(0, 0, 0, true, true)]
        [TestCase(0, 0, 0, false, false)]
        [TestCase(0.25, 0, 1, false, true)]
        [TestCase(1, 0, 1, false, true)]
        [TestCase(1, 1, 2, false, false)]
        [TestCase(1.25, 0, 1, false, false)]
        public void ForwardPointTruthTable(double point, double previous, double current, bool initial, bool expected)
        {
            Assert.That(TimelineTime.CrossesPoint(point, previous, current, initial), Is.EqualTo(expected));
        }

        [TestCase(0.25, true)]
        [TestCase(0.749999, true)]
        [TestCase(0.75, false)]
        [TestCase(0, false)]
        public void IntervalIsLeftClosedRightOpen(double time, bool expected)
        {
            Assert.That(TimelineTime.Contains(0.25, 0.5, time), Is.EqualTo(expected));
        }

        [TestCase(0, 0, 3, true, 4)]
        [TestCase(1, 0, 3, true, 3)]
        [TestCase(0.25, 0, 3.5, false, 4)]
        [TestCase(0.25, 0.25, 3.5, false, 3)]
        [TestCase(0, 1, 1, false, 0)]
        [TestCase(1, 0, 1, false, 1)]
        public void LoopCrossingsDoNotLoseCycles(double point, double previous, double current, bool initial, long expected)
        {
            Assert.That(TimelineTime.CountLoopOccurrences(point, 1, previous, current, initial), Is.EqualTo(expected));
        }

        [Test]
        public void ShortIntervalCrossedInOneAdvanceHasBothBoundaries()
        {
            Assert.That(TimelineTime.CrossesPoint(0.25, 0, 1), Is.True);
            Assert.That(TimelineTime.CrossesPoint(0.250001, 0, 1), Is.True);
            Assert.That(TimelineTime.Contains(0.25, 0.000001, 1), Is.False);
        }

        [Test]
        public void TouchingIntervalsAndPointsDoNotOverlap()
        {
            Assert.That(TimelineTime.Overlaps(0, 0.5, 0.5, 0.5), Is.False);
            Assert.That(TimelineTime.Overlaps(0, 1, 0.5, 0), Is.False);
            Assert.That(TimelineTime.Overlaps(0, 1, 0.5, 0.25), Is.True);
        }

        [Test]
        public void FrameRatesAreExplicitAndIndependent()
        {
            Assert.That(TimelineTime.FromFrames(30, 60), Is.EqualTo(0.5));
            Assert.That(TimelineTime.ToFrames(0.5, 24), Is.EqualTo(12));
            Assert.That(TimelineTime.ToFrames(0.25, 30), Is.EqualTo(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimelineTime.FromFrames(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimelineTime.ToFrames(double.NaN, 60));
            Assert.Throws<ArgumentException>(() => TimelineTime.CrossesPoint(0, 1, 0));
        }

        [Test]
        public void PersistentIdsAreNonEmptyCanonicalAndIndependentOfUnityObjects()
        {
            var original = StableId.New();
            var copy = StableId.New();
            Assert.That(StableId.IsValid(original), Is.True);
            Assert.That(copy, Is.Not.EqualTo(original));
            Assert.That(StableId.IsValid(new string('0', 32)), Is.False);
            Assert.That(StableId.IsValid("123"), Is.False);
        }

        [Test]
        public void SameTimeSortHonorsOldCycleThenExitPointEnterThenStableOrder()
        {
            const string idA = "00000000000000000000000000000001";
            const string idB = "00000000000000000000000000000002";
            var oldEndpoint = new BoundaryKey(1, 0, BoundaryPhase.Point, 99, 99, 99, idB);
            var nextZero = new BoundaryKey(1, 1, BoundaryPhase.Exit, 0, 0, 0, idA);
            Assert.That(oldEndpoint.CompareTo(nextZero), Is.LessThan(0));
            var exit = new BoundaryKey(1, 1, BoundaryPhase.Exit, 9, 9, 9, idB);
            var point = new BoundaryKey(1, 1, BoundaryPhase.Point, 0, 0, 0, idA);
            var enter = new BoundaryKey(1, 1, BoundaryPhase.Enter, 0, 0, 0, idA);
            Assert.That(exit.CompareTo(point), Is.LessThan(0));
            Assert.That(point.CompareTo(enter), Is.LessThan(0));
            Assert.That(point.CompareTo(new BoundaryKey(1, 1, BoundaryPhase.Point, 1, 0, 0, idA)), Is.LessThan(0));
            Assert.That(point.CompareTo(new BoundaryKey(1, 1, BoundaryPhase.Point, 0, 1, 0, idA)), Is.LessThan(0));
            Assert.That(point.CompareTo(new BoundaryKey(1, 1, BoundaryPhase.Point, 0, 0, 1, idA)), Is.LessThan(0));
            Assert.That(point.CompareTo(new BoundaryKey(1, 1, BoundaryPhase.Point, 0, 0, 0, idB)), Is.LessThan(0));
        }
    }
}
