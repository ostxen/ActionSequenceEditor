using NBC.ActionEditor.Infrastructure.Documents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public class StorageSafetyTests
    {
        private sealed class Files : ICommitFiles
        {
            public readonly Dictionary<string, byte[]> Data = new();
            public string Failure;
            public int Writes;
            public bool Exists(string path) => Data.ContainsKey(path);
            public byte[] Read(string path) => Data[path];
            public void Write(string path, byte[] bytes)
            {
                Writes++;
                if (Failure == "write") throw new UnauthorizedAccessException();
                Data.Add(path, Failure == "corrupt" ? new byte[] { 0 } : bytes);
            }
            public void Replace(string staged, string target)
            {
                if (Failure == "replace") throw new IOException();
                Data[target] = Data[staged];
                Data.Remove(staged);
            }
            public void Move(string staged, string target) { Data.Add(target, Data[staged]); Data.Remove(staged); }
            public void Delete(string path) => Data.Remove(path);
        }

        [TestCase("write")]
        [TestCase("replace")]
        [TestCase("corrupt")]
        [TestCase("serialize")]
        [TestCase("invalid")]
        public void FailureNeverReplacesLastValidBytes(string failure)
        {
            var files = new Files { Failure = failure };
            files.Data["document"] = new byte[] { 1, 2, 3 };
            Assert.Catch(() => new AtomicFileCommit(files).Save("document", () =>
            {
                if (failure == "serialize") throw new InvalidOperationException();
                return failure == "invalid" ? Array.Empty<byte>() : new byte[] { 4, 5, 6 };
            }, bytes => Assert.That(bytes.Length, Is.EqualTo(3))));
            Assert.That(files.Data["document"], Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(files.Data.Count, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedSuccessAndExternalChangeHaveDefinedOutcomes()
        {
            var files = new Files();
            var store = new AtomicFileCommit(files);
            store.Save("document", () => new byte[] { 1 }, _ => { });
            store.Save("document", () => new byte[] { 2 }, _ => { }, new byte[] { 1 });
            Assert.That(files.Data["document"].Single(), Is.EqualTo(2));
            Assert.Throws<IOException>(() => store.Save("document", () => new byte[] { 3 }, _ => { }, new byte[] { 1 }));
            Assert.That(files.Data["document"].Single(), Is.EqualTo(2));
        }

        [Test]
        public void PhysicalCommitPreservesOriginalOnValidationFailureAndLockedTarget()
        {
            var root = Path.Combine(Path.GetTempPath(), "ActionEditorCommit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var target = Path.Combine(root, "document.bin");
            try
            {
                var store = new AtomicFileCommit();
                store.Save(target, () => new byte[] { 1, 2 }, _ => { });
                store.Save(target, () => new byte[] { 3, 4 }, _ => { }, new byte[] { 1, 2 });
                Assert.Throws<InvalidDataException>(() => store.Save(target, () => new byte[] { 5 }, _ => throw new InvalidDataException()));
                using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var failure = Assert.Throws<IOException>(() => store.Save(target, () => new byte[] { 6 }, _ => { }));
                    Assert.That(failure.Message, Does.Contain("HRESULT"));
                    Assert.That(failure.InnerException, Is.TypeOf<IOException>());
                }
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 3, 4 }));
                Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1));
                store.Save(target, () => new byte[] { 7, 8 }, _ => { }, new byte[] { 3, 4 });
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 7, 8 }));
                Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1));
            }
            finally
            {
                if (File.Exists(target)) File.Delete(target);
                Directory.Delete(root);
            }
        }
        [Test] public void CreateOnlyCommitRejectsExistingTargetWithoutReplacingIt()
        {
            var files = new Files(); files.Data["document"] = new byte[] { 1 };
            Assert.Throws<IOException>(() => new AtomicFileCommit(files).Save("document", () => new byte[] { 2 }, _ => { }, createOnly: true));
            Assert.That(files.Data["document"], Is.EqualTo(new byte[] { 1 })); Assert.That(files.Writes, Is.Zero);
        }
        [Test] public void ImportFailureKeepsCommittedPathBaselineAndExactRevisionWithoutReplaying()
        {
            const string path = "Assets/ActionEditor/StorageFixture.json";
            var files = new Files(); var imports = 0;
            var store = new JsonDocumentRepository(files, _ => { imports++; throw new InvalidOperationException("Injected import failure"); });
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create(store);
            var memory = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                session.Open(new JsonDocumentRepository().ReadSource(memory)); session.Edit("change", () => ((FeatureSampleDocument)session.Document).Version = 7);
                var source = session.Document;
                var failure = Assert.Throws<IOException>(() => session.Save(path));
                Assert.That(failure.Message, Does.Contain("字节已提交")); Assert.That(imports, Is.EqualTo(1));
                Assert.That(files.Writes, Is.EqualTo(1)); Assert.That(session.Path, Is.EqualTo(path));
                Assert.That(session.Document, Is.SameAs(source)); Assert.That(session.IsDirty, Is.False);
                var committed = files.Data[path].ToArray();
                using var recovered = NBC.ActionEditor.Composition.DocumentComposition.Create(store); recovered.RestoreRecovery(session.CaptureRecovery());
                recovered.Edit("change", () => ((FeatureSampleDocument)recovered.Document).Version = 8);
                files.Data[path] = new byte[] { 42 };
                Assert.Throws<IOException>(() => recovered.Save());
                Assert.That(files.Data[path], Is.EqualTo(new byte[] { 42 })); Assert.That(files.Writes, Is.EqualTo(1));
                files.Data[path] = committed;
                Assert.Throws<IOException>(() => recovered.Save());
                Assert.That(imports, Is.EqualTo(2)); Assert.That(files.Writes, Is.EqualTo(2)); Assert.That(recovered.IsDirty, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void StoreRejectsMetaAndDirectoryTargetsAndSaveDoesNotEndActiveGesture()
        {
            var files = new Files(); var store = new JsonDocumentRepository(files, _ => { });
            const string path = "Assets/ActionEditor/StorageFixture.json";
            files.Data[path + ".meta"] = new byte[] { 1 };
            Assert.Throws<IOException>(() => store.Commit(path, new FeatureSampleDocument(), null));
            Assert.That(files.Writes, Is.Zero);
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create(store);
            var memory = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                session.Open(new JsonDocumentRepository().ReadSource(memory)); var source = session.Document; using var token = session.BeginEdit(this);
                ((FeatureSampleDocument)source).Version = 9;
                Assert.Throws<InvalidOperationException>(() => session.Save(path));
                Assert.That(token.IsActive, Is.True); Assert.That(session.Document, Is.SameAs(source)); Assert.That(files.Writes, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
    }
}
