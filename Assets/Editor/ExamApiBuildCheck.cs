using System;
using ExamCards.Exam.Api;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ExamCards.Editor
{
    /// <summary>
    /// Stops a build whose backend URL can't work in it: an invalid URL fails every build, and plain
    /// http:// fails a release build, because <c>insecureHttpOption</c> only allows it in development builds.
    /// </summary>
    sealed class ExamApiBuildCheck : IPreprocessBuildWithReport
    {
        const string SettingsPath = "Assets/Resources/" + ExamApiSettings.ResourceName + ".asset";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ExamApiSettings>(SettingsPath);
            if (settings == null)
                throw new BuildFailedException($"{SettingsPath} is missing. Run Tools ▸ ExamCards ▸ Setup PickOne Scene.");

            if (!ExamApiSettings.TryValidateBaseUrl(settings.BaseUrl, out var error))
                throw new BuildFailedException($"{error} Set it in {SettingsPath}.");

            var development = (report.summary.options & BuildOptions.Development) != 0;
            var secure = settings.BaseUrl.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            if (!development && !secure)
                throw new BuildFailedException(
                    $"The API base URL '{settings.BaseUrl}' uses http://, which release builds block. " +
                    $"Set an https:// URL in {SettingsPath}, or make a Development Build.");
        }
    }
}
