#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrazyCars.Editor
{
    public static class CIBuild
    {
        public static void BuildAndroid()
        {
            CrazyCarsBootstrap.BuildScene(false);
            Directory.CreateDirectory("build/Android");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
            var options=new BuildPlayerOptions
            {
                scenes=new[]{CrazyCarsBootstrap.ScenePath},
                locationPathName="build/Android/CrazyCars.apk",
                target=BuildTarget.Android,
                options=BuildOptions.None
            };
            BuildReport report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Crazy Cars Android build failed: "+report.summary.result);
            Debug.Log("Crazy Cars APK built successfully.");
        }
    }
}
#endif
