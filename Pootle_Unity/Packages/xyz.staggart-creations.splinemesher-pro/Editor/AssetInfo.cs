using System;
using UnityEditor;
using UnityEngine;

namespace sc.splinemesher.pro.editor
{
    public static class AssetInfo
    {
        public const string ASSET_NAME = "Spline Mesher Pro";
        public const string ASSET_ABRV = "SM_PRO";
        public const string VERSION = "1.0.17";
        public const int ASSET_ID = 338468;
        public const string DOC_URL = "https://staggart.xyz/support/documentation/spline-mesher-pro";
        public const string FORUM_URL = "https://forum.unity.com/threads/1565389";
        public const string DISCORD_INVITE_URL = "https://staggart.xyz/support/discord/";
        
        public static void OpenInPackageManager()
        {
            Application.OpenURL("com.unity3d.kharma:content/" + ASSET_ID);
        }
        
        public static void OpenReviewsPage()
        {
            Application.OpenURL($"https://assetstore.unity.com/packages/slug/{ASSET_ID}?aid=1011l7Uk8&pubref=sm2editor#reviews");
        }
        
        internal static class VersionChecking
        {
            public static bool UPDATE_AVAILABLE
            {
                get => SessionState.GetBool(ASSET_ABRV + "_UPDATE_AVAILABLE", false);
                set => SessionState.SetBool(ASSET_ABRV + "_UPDATE_AVAILABLE", value);
            }
            public static string LATEST_AVAILABLE
            {
                get => SessionState.GetString(ASSET_ABRV + "_LATEST_AVAILABLE", AssetInfo.VERSION);
                set => SessionState.SetString(ASSET_ABRV + "_LATEST_AVAILABLE", value);
            }

            private static string apiResult;

            #if SM_DEV
            [MenuItem("Tools/Spline Mesher/Check for update")]
            #endif
            public static void CheckForUpdate()
            {
                //Test
                //UPDATE_AVAILABLE = true; return;
                
                //Default, in case of a fail
                UPDATE_AVAILABLE = false;
                
                //Offline
                if (Application.internetReachability == NetworkReachability.NotReachable) return;
                
                //Debug.Log("Checking for version update");
                
                var url = $"https://api.assetstore.unity3d.com/package/latest-version/{ASSET_ID}";

                using (System.Net.WebClient webClient = new System.Net.WebClient())
                {
                    webClient.DownloadStringCompleted += OnRetrievedAPIContent;
                    webClient.DownloadStringAsync(new System.Uri(url), apiResult);
                }
            }

            private class AssetStoreItem
            {
                public string name;
                public string version;
            }

            private static void OnRetrievedAPIContent(object sender, System.Net.DownloadStringCompletedEventArgs e)
            {
                if (e.Error == null && !e.Cancelled)
                {
                    string result = e.Result;

                    AssetStoreItem asset = (AssetStoreItem)JsonUtility.FromJson(result, typeof(AssetStoreItem));

                    LATEST_AVAILABLE = asset.version;
                    //LATEST_AVAILABLE = "9.9.9";

                    Version remoteVersion = new Version(LATEST_AVAILABLE);
                    Version installedVersion = new Version(AssetInfo.VERSION);

                    UPDATE_AVAILABLE = remoteVersion > installedVersion;

                    if (UPDATE_AVAILABLE)
                    {
                        //Debug.Log($"[{asset.name} v{installedVersion}] New version ({asset.version}) is available");
                    }
                }
            }
        }
    }
}