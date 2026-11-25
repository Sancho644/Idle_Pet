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
    }
}
