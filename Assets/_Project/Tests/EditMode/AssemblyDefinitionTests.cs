using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace IdlePet.Tests.EditMode
{
    public sealed class AssemblyDefinitionTests
    {
        [Test]
        public void EditorCompilesTheSetupAssembliesWithTheExpectedDependencies()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var domain = assemblies.Single(assembly => assembly.name == "IdlePet.Domain");
            var application = assemblies.Single(assembly => assembly.name == "IdlePet.Application");
            var tests = assemblies.Single(assembly => assembly.name == "IdlePet.Tests.EditMode");

            var testReferences = tests.assemblyReferences.Select(assembly => assembly.name);
            var domainUnityReferences = domain.compiledAssemblyReferences
                .Select(Path.GetFileName)
                .Where(name => name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                    name.StartsWith("UnityEditor", StringComparison.Ordinal));

            Assert.That(application.assemblyReferences.Select(assembly => assembly.name), Has.Member(domain.name));
            Assert.That(testReferences, Has.Member(domain.name).And.Member(application.name));
            Assert.That(domainUnityReferences, Is.Empty);
        }

        [Test]
        public void AuthoringAndEditorExtendTheAcyclicGraphWithoutEnteringDomainOrPlayerEditorCode()
        {
            var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var domain = editorAssemblies.Single(assembly => assembly.name == "IdlePet.Domain");
            var application = editorAssemblies.Single(assembly => assembly.name == "IdlePet.Application");
            var authoring = editorAssemblies.Single(assembly => assembly.name == "IdlePet.Authoring");
            var editor = editorAssemblies.Single(assembly => assembly.name == "IdlePet.Editor");
            var tests = editorAssemblies.Single(assembly => assembly.name == "IdlePet.Tests.EditMode");

            Assert.That(ProjectReferences(domain), Is.Empty);
            Assert.That(ProjectReferences(application), Is.EquivalentTo(new[] { domain.name }));
            Assert.That(ProjectReferences(authoring), Is.EquivalentTo(new[] { domain.name }));
            Assert.That(ProjectReferences(editor), Is.EquivalentTo(new[] { authoring.name, domain.name, application.name }));
            Assert.That(ProjectReferences(tests), Is.EquivalentTo(new[] { domain.name, application.name, authoring.name, editor.name }));

            var engineReferences = application.compiledAssemblyReferences.Select(Path.GetFileName)
                .Where(name => name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                    name.StartsWith("UnityEditor", StringComparison.Ordinal));
            Assert.That(engineReferences, Is.Empty);
            var playerNames = playerAssemblies.Select(assembly => assembly.name).ToArray();
            Assert.That(playerNames, Has.Member(authoring.name));
            Assert.That(playerNames, Has.No.Member(editor.name));
            Assert.That(playerNames, Has.No.Member(tests.name));
        }

        private static string[] ProjectReferences(Assembly assembly)
        {
            return assembly.assemblyReferences.Select(reference => reference.name)
                .Where(name => name.StartsWith("IdlePet.", StringComparison.Ordinal))
                .ToArray();
        }
    }
}
