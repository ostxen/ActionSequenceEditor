using System;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Presenters;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests.Presentation
{
    public sealed class AssemblyBoundaryTests
    {
        [Test] public void DomainAndSerializationKeepOneWayRuntimeDependencies()
        {
            var domain = typeof(NBC.ActionEditor.Domain.ActionDocument).Assembly;
            var serialization = typeof(NBC.ActionEditor.Serialization.ActionDocumentJsonFormat).Assembly;
            Assert.That(domain.GetName().Name, Is.EqualTo("NBC.ActionEditor.Domain"));
            Assert.That(serialization.GetName().Name, Is.EqualTo("NBC.ActionEditor.Serialization"));
            foreach (var assembly in new[] { domain, serialization })
                Assert.That(assembly.GetReferencedAssemblies().Where(reference => reference.Name.StartsWith("UnityEditor", StringComparison.Ordinal)
                    || reference.Name == "FullSerializer" || reference.Name == "NBC.ActionEditor.Editor"), Is.Empty);
            Assert.That(domain.GetReferencedAssemblies().Any(reference => reference.Name == serialization.GetName().Name), Is.False);
            Assert.That(serialization.GetReferencedAssemblies().Any(reference => reference.Name == domain.GetName().Name), Is.True);
        }

        [Test] public void ApplicationAndPresentationReferencesExcludeEditorAndControls()
        {
            var application = typeof(SettingsSession).Assembly;
            var presentation = typeof(SettingsPresenter).Assembly;
            Assert.That(application.GetName().Name, Is.EqualTo("NBC.ActionEditor.Application"));
            Assert.That(presentation.GetName().Name, Is.EqualTo("NBC.ActionEditor.Presentation"));
            foreach (var assembly in new[] { application, presentation })
            {
                var forbidden = assembly.GetReferencedAssemblies().Where(reference =>
                    reference.Name.StartsWith("UnityEditor", StringComparison.Ordinal)
                    || reference.Name.StartsWith("UnityEngine.UI", StringComparison.Ordinal)
                    || reference.Name.StartsWith("UnityEngine.IMGUIModule", StringComparison.Ordinal)
                    || reference.Name == "NBC.ActionEditor.Editor").ToArray();
                Assert.That(forbidden, Is.Empty, assembly.FullName);
            }
            Assert.That(application.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(application.GetReferencedAssemblies().Any(reference => reference.Name == presentation.GetName().Name), Is.False);
            Assert.That(presentation.GetReferencedAssemblies().Any(reference => reference.Name == application.GetName().Name), Is.True);
        }

        [Test] public void HeadlessLayersDoNotAcquireStaticFacadeFileOrWindowDependencies()
        {
            var root = Path.Combine(UnityEngine.Application.dataPath, "ActionEditor/Editor");
            foreach (var directory in new[] { "Application", "Presentation" })
            foreach (var path in Directory.GetFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                foreach (var forbidden in new[] { "UnityEditor", "UnityEngine.UIElements", "EditorWindow", "Event.current", "App.", "ActionEditorHost.", "File.", "AssetDatabase", "Selection.activeObject" })
                    Assert.That(source, Does.Not.Contain(forbidden), path);
            }
        }
    }
}
