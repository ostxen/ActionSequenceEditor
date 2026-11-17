using System;
using System.IO;
using System.Linq;
using ActionSequenceEditor.Application.Settings;
using ActionSequenceEditor.Presentation.Presenters;
using NUnit.Framework;

namespace ActionSequenceEditor.Tests.Presentation
{
    public sealed class AssemblyBoundaryTests
    {
        [Test] public void DomainAndSerializationKeepOneWayRuntimeDependencies()
        {
            var domain = typeof(ActionSequenceEditor.Model.ActionSequence).Assembly;
            var serialization = typeof(ActionSequenceEditor.Serialization.SequenceJsonFormat).Assembly;
            Assert.That(domain.GetName().Name, Is.EqualTo("ActionSequenceEditor.Runtime"));
            Assert.That(serialization, Is.SameAs(domain));
            foreach (var assembly in new[] { domain, serialization })
                Assert.That(assembly.GetReferencedAssemblies().Where(reference => reference.Name.StartsWith("UnityEditor", StringComparison.Ordinal)
                    || reference.Name == "FullSerializer" || reference.Name == "ActionSequenceEditor.Editor"
                    || reference.Name == "nunit.framework" || reference.Name.EndsWith("TestRunner", StringComparison.Ordinal)), Is.Empty);
        }

        [Test] public void ApplicationAndPresentationReferencesExcludeEditorAndControls()
        {
            var application = typeof(SettingsSession).Assembly;
            var presentation = typeof(SettingsPresenter).Assembly;
            Assert.That(application.GetName().Name, Is.EqualTo("ActionSequenceEditor.Editor"));
            Assert.That(presentation, Is.SameAs(application));
            Assert.That(typeof(ActionSequenceEditor.Verification.ProjectSyncCommands).Assembly, Is.SameAs(application));
            Assert.That(typeof(AssemblyBoundaryTests).Assembly, Is.Not.SameAs(application));
            foreach (var assembly in new[] { application, presentation })
            {
                var forbidden = assembly.GetReferencedAssemblies().Where(reference =>
                    reference.Name == "nunit.framework" || reference.Name.EndsWith("TestRunner", StringComparison.Ordinal)).ToArray();
                Assert.That(forbidden, Is.Empty, assembly.FullName);
            }
            Assert.That(application.GetReferencedAssemblies().Any(reference => reference.Name == "ActionSequenceEditor.Runtime"), Is.True);
            var root = Path.Combine(UnityEngine.Application.dataPath, "ActionSequenceEditor");
            Assert.That(Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension),
                Is.EquivalentTo(new[] { "ActionSequenceEditor.Runtime", "ActionSequenceEditor.Editor" }));
        }

        [Test] public void HeadlessLayersDoNotAcquireStaticFacadeFileOrWindowDependencies()
        {
            var root = Path.Combine(UnityEngine.Application.dataPath, "ActionSequenceEditor/Editor");
            foreach (var directory in new[] { "Application", "Presentation" })
            foreach (var path in Directory.GetFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                foreach (var forbidden in new[] { "UnityEditor", "UnityEngine.UIElements", "EditorWindow", "Event.current", "App.", "ActionSequenceEditorHost.", "File.", "AssetDatabase", "Selection.activeObject" })
                    Assert.That(source, Does.Not.Contain(forbidden), path);
            }
        }
    }
}
