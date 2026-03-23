using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class iOSPostBuild
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;

        // Allow plain TCP connections (needed for MQTT on port 1883)
        PlistElementDict ats = root.CreateDict("NSAppTransportSecurity");
        ats.SetBoolean("NSAllowsArbitraryLoads", true);

        // Required for iOS 14+ local network access
        root.SetString("NSLocalNetworkUsageDescription",
            "This app connects to a local MQTT broker for real-time bartender control.");

        plist.WriteToFile(plistPath);

        Debug.Log("✓ iOS post-build: ATS and local network permissions added to Info.plist");
    }
}
