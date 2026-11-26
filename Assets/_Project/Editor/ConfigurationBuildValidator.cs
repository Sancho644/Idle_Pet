using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Authoring.Mapping;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.SceneManagement;

namespace IdlePet.Editor
{
    public sealed class ConfigurationBuildValidator : BuildPlayerProcessor
    {
        private readonly Func<string[], IReadOnlyList<ConfigurationIssue>> _validate;

        public ConfigurationBuildValidator() : this(GameConfigurationValidation.CheckProduction)
        {
        }

        internal ConfigurationBuildValidator(Func<string[], IReadOnlyList<ConfigurationIssue>> validate)
        {
            _validate = validate;
        }

        public override int callbackOrder => 0;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            ValidateBuild(context.BuildPlayerOptions);
        }

        internal void ValidateBuild(BuildPlayerOptions options)
        {
            string[] scenes = options.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                scenes = new[] { SceneManager.GetActiveScene().path };
            }

            var errors = _validate(scenes).Where(issue => issue.Severity == ConfigurationSeverity.Error).ToArray();
            if (errors.Length != 0)
            {
                throw new BuildFailedException("Idle Pet configuration is invalid.\n" + string.Join("\n", errors.Select(issue => issue.ToString())));
            }
        }
    }
}
