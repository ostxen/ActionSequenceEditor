using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace NBC.ActionEditor.Tests
{
    public class LegacySafetyTests
    {
        [Test]
        public void LegacySaveEntryPointIsExplicitlyReadOnly()
        {
            Assert.Throws<InvalidOperationException>(() => App.SaveAsset());
        }

        [Test]
        public void LegacyAutoSaveNeverUpdatesTimestampOrWrites()
        {
            var before = App.LastSaveTime;
            App.AutoSave();
            Assert.That(App.LastSaveTime, Is.EqualTo(before));
        }

        [TestCase(60, 59, false)]
        [TestCase(60, 60, true)]
        [TestCase(120, 119, false)]
        [TestCase(120, 120, true)]
        [TestCase(120, 360, true)]
        [TestCase(0, 360, false)]
        public void AutoSaveUsesTotalMonotonicDuration(double interval, double elapsed, bool expected)
        {
            Assert.That(NBC.ActionEditor.Model.AutoSaveSchedule.IsDue(10, 10 + elapsed, interval), Is.EqualTo(expected));
        }

        [Test]
        public void FailedLegacyOpenDoesNotChangeDocumentOrTarget()
        {
            var previous = App.TextAsset;
            var valid = new TextAsset(Json.Serialize(new LegacyBaselineTests.BaselineAsset()));
            var invalid = new TextAsset("not json");
            try
            {
                App.TextAsset = valid;
                var document = App.AssetData;
                LogAssert.Expect(LogType.Error, new Regex(".*", RegexOptions.Singleline));
                App.TextAsset = invalid;
                Assert.That(App.TextAsset, Is.SameAs(valid));
                Assert.That(App.AssetData, Is.SameAs(document));
            }
            finally
            {
                App.TextAsset = previous;
                UnityEngine.Object.DestroyImmediate(valid);
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }
    }
}
