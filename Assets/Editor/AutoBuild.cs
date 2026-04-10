using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class AutoBuild
{
    [MenuItem("Build/iOS Auto Build")]
    public static void BuildiOS()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
            locationPathName = "Build",
            target = BuildTarget.iOS,
            options = BuildOptions.AcceptExternalModificationsToPlayer
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log("iOS build succeeded!");
        else
            Debug.LogError("iOS build failed");
    }
}