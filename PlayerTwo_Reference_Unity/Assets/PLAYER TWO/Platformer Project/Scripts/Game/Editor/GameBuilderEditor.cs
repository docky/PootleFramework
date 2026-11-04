using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	public class GameBuilderEditor : EditorWindow
	{
		[System.Flags]
		public enum TargetPlatform
		{
			Windows = 1 << 0,
			Linux = 1 << 1,
			Mac = 1 << 2,
			WebGL = 1 << 3,
			Android = 1 << 4,
		}

		protected const string OutputFolder = "Builds";
		protected const string LocalServerUrl = "http://localhost:8080";
		protected const int LocalServerPort = 8080;
		protected const string TargetPlatformsKey =
			"PLAYERTWO.PlatformerProject.GameBuilder.TargetPlatforms";
		protected const string CompressToZipKey =
			"PLAYERTWO.PlatformerProject.GameBuilder.CompressToZip";
		protected const string RemoveDebugFoldersKey =
			"PLAYERTWO.PlatformerProject.GameBuilder.RemoveDebugFolders";
		protected const string BurstDebugFolderSuffix = "BurstDebugInformation_DoNotShip";

		protected static readonly GUIContent TargetPlatformsContent = new GUIContent(
			"Target Platforms",
			"Choose which platforms Build All will generate."
		);

		protected static readonly GUIContent CompressToZipContent = new GUIContent(
			"Compress To Zip",
			"Compress each non-Android platform build folder into its own zip file."
		);

		protected static readonly GUIContent RemoveDebugFoldersContent = new GUIContent(
			"Remove Debug Folders",
			"Delete BurstDebugInformation_DoNotShip folders from Builds after building."
		);

		[SerializeField]
		[Tooltip("Choose which platforms Build All will generate.")]
		protected TargetPlatform targetPlatforms = TargetPlatform.Windows;

		[SerializeField]
		[Tooltip("Compress each non-Android platform build folder into its own zip file.")]
		protected bool compressToZip;

		[SerializeField]
		[Tooltip("Delete BurstDebugInformation_DoNotShip folders from Builds after building.")]
		protected bool removeDebugFolders;

		[MenuItem("Window/Platformer Project/Game Builder")]
		public static void Open()
		{
			GetWindow<GameBuilderEditor>("Game Builder");
		}

		protected virtual void OnGUI()
		{
			DrawOptions();
			DrawActions();
		}

		protected virtual void OnEnable()
		{
			LoadSettings();
		}

		protected virtual void OnDisable()
		{
			SaveSettings();
		}

		protected virtual void DrawOptions()
		{
			EditorGUI.BeginChangeCheck();

			targetPlatforms = (TargetPlatform)
				EditorGUILayout.EnumFlagsField(TargetPlatformsContent, targetPlatforms);
			compressToZip = EditorGUILayout.Toggle(CompressToZipContent, compressToZip);
			removeDebugFolders = EditorGUILayout.Toggle(
				RemoveDebugFoldersContent,
				removeDebugFolders
			);

			if (EditorGUI.EndChangeCheck())
				SaveSettings();
		}

		protected virtual void DrawActions()
		{
			EditorGUILayout.Space();

			if (GUILayout.Button("Run With Python Server"))
				RunWithPythonServer();

			if (GUILayout.Button("Remove Debug Folders"))
				PromptRemoveDebugFolders();

			if (GUILayout.Button("Compress To Zip"))
				PromptCompressToZip();

			if (GUILayout.Button("Build All"))
				PromptBuildAll();
		}

		protected virtual void RunWithPythonServer()
		{
			if (!BuildWebGL())
				return;

			FinalizeBuildFolders("webgl");

			if (!StartPythonServer(GetPlatformFolder("webgl")))
				return;

			OpenLocalServerUrl();
		}

		protected virtual void PromptBuildAll()
		{
			if (ConfirmBuildAll())
				BuildAll();
		}

		protected virtual void PromptRemoveDebugFolders()
		{
			if (ConfirmRemoveDebugFolders())
				RemoveDebugFolders();
		}

		protected virtual void PromptCompressToZip()
		{
			if (ConfirmCompressToZip())
				CompressExistingBuildsToZip();
		}

		protected virtual void BuildAll()
		{
			var builtPlatforms = new System.Collections.Generic.List<string>();

			if (IsSelected(TargetPlatform.Windows) && BuildWindows())
				builtPlatforms.Add("windows");

			if (IsSelected(TargetPlatform.Linux) && BuildLinux())
				builtPlatforms.Add("linux");

			if (IsSelected(TargetPlatform.Mac) && BuildMac())
				builtPlatforms.Add("mac");

			if (IsSelected(TargetPlatform.WebGL) && BuildWebGL())
				builtPlatforms.Add("webgl");

			if (IsSelected(TargetPlatform.Android))
				BuildAndroid();

			FinalizeBuildFolders(builtPlatforms.ToArray());
		}

		protected virtual bool BuildWindows()
		{
			var platform = "windows";
			var folder = PreparePlatformFolder(platform);
			var path = System.IO.Path.Combine(folder, $"{GetProductName()}.exe");
			return BuildPlayer(path, BuildTarget.StandaloneWindows64);
		}

		protected virtual bool BuildLinux()
		{
			var platform = "linux";
			var folder = PreparePlatformFolder(platform);
			var path = System.IO.Path.Combine(folder, GetProductName());
			return BuildPlayer(path, BuildTarget.StandaloneLinux64);
		}

		protected virtual bool BuildMac()
		{
			var platform = "mac";
			var folder = PreparePlatformFolder(platform);
			var path = System.IO.Path.Combine(folder, $"{GetProductName()}.app");
			return BuildPlayer(path, BuildTarget.StandaloneOSX);
		}

		protected virtual bool BuildWebGL()
		{
			var platform = "webgl";
			var path = PreparePlatformFolder(platform);
			return BuildPlayer(path, BuildTarget.WebGL);
		}

		protected virtual bool BuildAndroid()
		{
			System.IO.Directory.CreateDirectory(OutputFolder);

			var path = GetAndroidBuildPath();
			DeleteFileIfExists(path);

			return BuildPlayer(path, BuildTarget.Android);
		}

		protected virtual bool BuildPlayer(string locationPathName, BuildTarget buildTarget)
		{
			var scenes = GetEnabledScenes();

			if (scenes.Length == 0)
			{
				Debug.LogError("[GameBuilder] No enabled scenes found in Build Settings.");
				return false;
			}

			var options = new BuildPlayerOptions
			{
				scenes = scenes,
				locationPathName = locationPathName,
				target = buildTarget,
				options = BuildOptions.None,
			};

			var report = BuildPipeline.BuildPlayer(options);
			return LogBuildResult(report, buildTarget, locationPathName);
		}

		protected virtual string[] GetEnabledScenes()
		{
			var scenes = new System.Collections.Generic.List<string>();

			foreach (var scene in EditorBuildSettings.scenes)
			{
				if (scene.enabled)
					scenes.Add(scene.path);
			}

			return scenes.ToArray();
		}

		protected virtual bool LogBuildResult(
			BuildReport report,
			BuildTarget buildTarget,
			string path
		)
		{
			if (report.summary.result == BuildResult.Succeeded)
			{
				Debug.Log($"[GameBuilder] {buildTarget} build completed: {path}");
				return true;
			}

			Debug.LogError($"[GameBuilder] {buildTarget} build failed: {report.summary.result}");
			return false;
		}

		protected virtual bool IsSelected(TargetPlatform platform)
		{
			return (targetPlatforms & platform) == platform;
		}

		protected virtual bool ConfirmBuildAll()
		{
			var selectedCount = GetSelectedTargetPlatformCount();
			var message = GetBuildAllConfirmationMessage(selectedCount);

			return EditorUtility.DisplayDialog("Build All", message, "Build", "Cancel");
		}

		protected virtual bool ConfirmRemoveDebugFolders()
		{
			var message =
				"This will delete all BurstDebugInformation_DoNotShip folders found inside the Builds folder.\n\nDo you want to continue?";

			return EditorUtility.DisplayDialog("Remove Debug Folders", message, "Delete", "Cancel");
		}

		protected virtual bool ConfirmCompressToZip()
		{
			var message =
				"This will delete the current zip files in the Builds folder and create new zip files from existing platform build folders.\n\nDo you want to continue?";

			return EditorUtility.DisplayDialog("Compress To Zip", message, "Compress", "Cancel");
		}

		protected virtual int GetSelectedTargetPlatformCount()
		{
			var count = 0;

			if (IsSelected(TargetPlatform.Windows))
				count++;

			if (IsSelected(TargetPlatform.Linux))
				count++;

			if (IsSelected(TargetPlatform.Mac))
				count++;

			if (IsSelected(TargetPlatform.WebGL))
				count++;

			if (IsSelected(TargetPlatform.Android))
				count++;

			return count;
		}

		protected virtual string GetBuildAllConfirmationMessage(int selectedCount)
		{
			if (selectedCount > 1)
			{
				return $"You selected {selectedCount} target platforms. Building multiple targets can take a while.\n\nDo you want to continue?";
			}

			return "This build process can take a while.\n\nDo you want to continue?";
		}

		protected virtual void LoadSettings()
		{
			targetPlatforms = (TargetPlatform)
				EditorPrefs.GetInt(TargetPlatformsKey, (int)TargetPlatform.WebGL);
			compressToZip = EditorPrefs.GetBool(CompressToZipKey, false);
			removeDebugFolders = EditorPrefs.GetBool(RemoveDebugFoldersKey, false);
		}

		protected virtual void SaveSettings()
		{
			EditorPrefs.SetInt(TargetPlatformsKey, (int)targetPlatforms);
			EditorPrefs.SetBool(CompressToZipKey, compressToZip);
			EditorPrefs.SetBool(RemoveDebugFoldersKey, removeDebugFolders);
		}

		protected virtual string PreparePlatformFolder(string platform)
		{
			var folder = GetPlatformFolder(platform);

			DeleteDirectoryIfExists(folder);
			System.IO.Directory.CreateDirectory(folder);

			return folder;
		}

		protected virtual string GetPlatformFolder(string platform)
		{
			return System.IO.Path.Combine(OutputFolder, GetPlatformOutputName(platform));
		}

		protected virtual string GetPlatformOutputName(string platform)
		{
			return $"{GetProductName()}-{platform}";
		}

		protected virtual string GetAndroidBuildPath()
		{
			return System.IO.Path.Combine(OutputFolder, $"{GetPlatformOutputName("android")}.apk");
		}

		protected virtual string GetProductName()
		{
			return SanitizeFileName(PlayerSettings.productName);
		}

		protected virtual string SanitizeFileName(string value)
		{
			foreach (var invalidCharacter in System.IO.Path.GetInvalidFileNameChars())
				value = value.Replace(invalidCharacter, '-');

			return value;
		}

		protected virtual void FinalizeBuildFolders(params string[] platforms)
		{
			RemoveDebugFoldersIfNeeded();
			CompressPlatformFoldersIfNeeded(platforms);
		}

		protected virtual void RemoveDebugFoldersIfNeeded()
		{
			if (removeDebugFolders)
				RemoveDebugFolders();
		}

		protected virtual void RemoveDebugFolders()
		{
			if (!System.IO.Directory.Exists(OutputFolder))
				return;

			var folders = System.IO.Directory.GetDirectories(
				OutputFolder,
				$"*{BurstDebugFolderSuffix}",
				System.IO.SearchOption.AllDirectories
			);

			foreach (var folder in folders)
				DeleteDirectoryIfExists(folder);

			Debug.Log($"[GameBuilder] Removed {folders.Length} Burst debug folders.");
		}

		protected virtual void CompressPlatformFoldersIfNeeded(string[] platforms)
		{
			if (!compressToZip)
				return;

			CompressPlatformFolders(platforms);
		}

		protected virtual void CompressExistingBuildsToZip()
		{
			if (!System.IO.Directory.Exists(OutputFolder))
			{
				Debug.LogWarning("[GameBuilder] Builds folder was not found.");
				return;
			}

			DeleteZipFiles();
			CompressPlatformFolders(GetExistingBuildPlatforms());
		}

		protected virtual void DeleteZipFiles()
		{
			var zipFiles = System.IO.Directory.GetFiles(
				OutputFolder,
				"*.zip",
				System.IO.SearchOption.TopDirectoryOnly
			);

			foreach (var zipFile in zipFiles)
				DeleteFileIfExists(zipFile);

			Debug.Log($"[GameBuilder] Deleted {zipFiles.Length} zip files.");
		}

		protected virtual string[] GetExistingBuildPlatforms()
		{
			var platforms = new System.Collections.Generic.List<string>();

			AddExistingBuildPlatform(platforms, "windows");
			AddExistingBuildPlatform(platforms, "linux");
			AddExistingBuildPlatform(platforms, "mac");
			AddExistingBuildPlatform(platforms, "webgl");

			return platforms.ToArray();
		}

		protected virtual void AddExistingBuildPlatform(
			System.Collections.Generic.List<string> platforms,
			string platform
		)
		{
			if (System.IO.Directory.Exists(GetPlatformFolder(platform)))
				platforms.Add(platform);
		}

		protected virtual void CompressPlatformFolders(string[] platforms)
		{
			foreach (var platform in platforms)
				CompressPlatformFolder(platform);
		}

		protected virtual void CompressPlatformFolder(string platform)
		{
			var folder = GetPlatformFolder(platform);
			var zipPath = $"{folder}.zip";

			DeleteFileIfExists(zipPath);
			System.IO.Compression.ZipFile.CreateFromDirectory(
				folder,
				zipPath,
				System.IO.Compression.CompressionLevel.Optimal,
				false
			);

			Debug.Log($"[GameBuilder] Compressed build: {zipPath}");
		}

		protected virtual bool StartPythonServer(string workingDirectory)
		{
			try
			{
				var process = System.Diagnostics.Process.Start(
					CreatePythonServerStartInfo(workingDirectory)
				);

				if (process == null)
				{
					Debug.LogError("[GameBuilder] Failed to start Python server.");
					return false;
				}

				Debug.Log($"[GameBuilder] Python server started at {LocalServerUrl}");
				return true;
			}
			catch (System.Exception exception)
			{
				Debug.LogError($"[GameBuilder] Failed to start Python server: {exception.Message}");
				return false;
			}
		}

		protected virtual System.Diagnostics.ProcessStartInfo CreatePythonServerStartInfo(
			string workingDirectory
		)
		{
			return new System.Diagnostics.ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = $"/k python -m http.server {LocalServerPort}",
				WorkingDirectory = workingDirectory,
				UseShellExecute = true,
				CreateNoWindow = false,
			};
		}

		protected virtual void OpenLocalServerUrl()
		{
			Application.OpenURL(LocalServerUrl);
		}

		protected virtual void DeleteDirectoryIfExists(string path)
		{
			if (System.IO.Directory.Exists(path))
				System.IO.Directory.Delete(path, true);
		}

		protected virtual void DeleteFileIfExists(string path)
		{
			if (System.IO.File.Exists(path))
				System.IO.File.Delete(path);
		}
	}
}
