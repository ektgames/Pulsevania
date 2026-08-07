#if UNITY_IPHONE || UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Pulsevania.EditorTools
{
    public static class IOSAdMobDependencyPostProcessor
    {
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            string podfilePath = Path.Combine(buildPath, "Podfile");
            Debug.Log("[IOSAdMobDependencyPostProcessor] Verifying Podfile at: " + podfilePath);

            string defaultPodfile = @"source 'https://github.com/CocoaPods/Specs.git'
platform :ios, '13.0'

target 'UnityFramework' do
  pod 'Google-Mobile-Ads-SDK', '~> 13.4'
  pod 'GoogleUserMessagingPlatform', '3.1.0'
end

target 'Unity-iPhone' do
  pod 'Google-Mobile-Ads-SDK', '~> 13.4'
  pod 'GoogleUserMessagingPlatform', '3.1.0'
end
";

            if (!File.Exists(podfilePath))
            {
                Debug.Log("[IOSAdMobDependencyPostProcessor] Podfile not found. Creating default Podfile for Google Mobile Ads & UMP...");
                File.WriteAllText(podfilePath, defaultPodfile);
                Debug.Log("[IOSAdMobDependencyPostProcessor] Successfully created Podfile.");
            }
            else
            {
                string content = File.ReadAllText(podfilePath);
                if (!content.Contains("Google-Mobile-Ads-SDK") || !content.Contains("UnityFramework"))
                {
                    Debug.Log("[IOSAdMobDependencyPostProcessor] Updating existing Podfile with complete target specifications...");
                    File.WriteAllText(podfilePath, defaultPodfile);
                    Debug.Log("[IOSAdMobDependencyPostProcessor] Successfully updated Podfile.");
                }
            }
        }
    }
}
#endif
