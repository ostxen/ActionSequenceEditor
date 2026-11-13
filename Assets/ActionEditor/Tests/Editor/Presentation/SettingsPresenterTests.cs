using System;
using System.Collections.Generic;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests.Presentation
{
    public sealed class SettingsPresenterTests
    {
        private static EditorSettings Settings(bool magnet = true) =>
            new EditorSettings(TimeStepMode.Seconds, .1f, 30, magnet, true, 0, "Assets/");

        private sealed class Store : IPreferencesStore
        {
            public EditorSettings Value = Settings();
            public int Writes;
            public bool Fail;
            public EditorSettings Read() => Value;
            public void Write(EditorSettings settings)
            {
                if (Fail) throw new InvalidOperationException("Storage unavailable");
                Value = settings; Writes++;
            }
        }

        private sealed class View : ISettingsView
        {
            public readonly List<EditorSettings> Frames = new List<EditorSettings>();
            public string Error;
            public EditorSettings State => Frames[Frames.Count - 1];
            public void Render(EditorSettings state) => Frames.Add(state);
            public void ShowError(string message) => Error = message;
        }

        [Test] public void TwoViewsShareCommitsButKeepDraftAndCancelIndependent()
        {
            var store = new Store(); var session = new SettingsSession(store);
            var first = new View(); var second = new View();
            using var a = new SettingsPresenter(session, first);
            using var b = new SettingsPresenter(session, second);
            a.Preview(Settings(false));
            Assert.That(first.State.MagnetSnapping, Is.False);
            Assert.That(second.State.MagnetSnapping, Is.True);
            Assert.That(store.Writes, Is.Zero);
            b.Cancel();
            Assert.That(first.State.MagnetSnapping, Is.False);
            Assert.That(a.Apply().Succeeded, Is.True);
            Assert.That(second.State.MagnetSnapping, Is.False);
            Assert.That(store.Writes, Is.EqualTo(1));
            a.Preview(Settings()); a.Cancel();
            Assert.That(first.State.MagnetSnapping, Is.False);
            Assert.That(store.Writes, Is.EqualTo(1));
            a.Preview(Settings(false));
            Assert.That(a.Apply().Changed, Is.False);
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test] public void FailedWriteRetainsSharedValueAndRetryUsesTheSameDraft()
        {
            var store = new Store { Fail = true }; var session = new SettingsSession(store);
            var view = new View(); var notifications = 0;
            session.Changed += () => notifications++;
            using var presenter = new SettingsPresenter(session, view);
            presenter.Preview(Settings(false));
            Assert.That(presenter.Apply().Succeeded, Is.False);
            Assert.That(session.Current.MagnetSnapping, Is.True);
            Assert.That(view.Error, Is.EqualTo("Storage unavailable"));
            Assert.That(notifications, Is.Zero);
            store.Fail = false;
            Assert.That(presenter.Apply().Succeeded, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(view.Error, Is.Null);
        }

        [Test] public void StaleDraftCannotOverwriteAnotherWindowsCommit()
        {
            var store = new Store(); var session = new SettingsSession(store);
            var first = new View(); var second = new View();
            using var a = new SettingsPresenter(session, first);
            using var b = new SettingsPresenter(session, second);
            a.Preview(Settings()); b.Preview(Settings(false)); b.Apply();
            Assert.That(a.Apply().Succeeded, Is.False);
            Assert.That(session.Current.MagnetSnapping, Is.False);
            Assert.That(first.State.MagnetSnapping, Is.False);
            Assert.That(first.Error, Does.Contain("其他窗口"));
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test] public void DisposingPresenterReleasesSubscriptionAndRejectsFurtherInput()
        {
            var session = new SettingsSession(new Store()); var view = new View();
            var presenter = new SettingsPresenter(session, view);
            presenter.Dispose(); presenter.Dispose();
            session.Commit(Settings(false));
            Assert.That(view.Frames.Count, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => presenter.Preview(Settings()));
        }

        [Test] public void InvalidPreferencesFailBeforeAnyStorageWrite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EditorSettings(TimeStepMode.Seconds, float.NaN, 30, true, true, 0, "Assets/"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EditorSettings(TimeStepMode.Seconds, .1f, 0, true, true, 0, "Assets/"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EditorSettings(TimeStepMode.Seconds, .1f, 30, true, true, 4, "Assets/"));
            Assert.Throws<ArgumentException>(() => new EditorSettings(TimeStepMode.Seconds, .1f, 30, true, true, 0, " "));
        }

        [Test] public void NotificationFailureDoesNotUndoPersistedStateOrBlockOtherSubscribers()
        {
            var store = new Store(); var session = new SettingsSession(store); var received = 0;
            session.Changed += () => throw new InvalidOperationException("View unavailable");
            session.Changed += () => received++;
            var result = session.Commit(Settings(false));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Warning, Is.EqualTo("View unavailable"));
            Assert.That(session.Current, Is.SameAs(store.Value));
            Assert.That(received, Is.EqualTo(1));
            Assert.That(session.Commit(Settings()).Succeeded, Is.True);
        }
    }
}
