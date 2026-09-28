using System;
using System.IO;
using Newtonsoft.Json;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Model;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Parser;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.IO;
using Unity.Services.Multiplayer.Editor.Shared.Assets;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using PathIO = System.IO.Path;

namespace Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Model
{
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.services.multiplayer@1.0/manual/Matchmaker/Authoring/index.html")]
    class MatchmakerAsset : ScriptableObject, IPath, ISerializationCallbackReceiver
    {
        const string k_DefaultFileName = "Matchmaker";
        string m_Path;

        public string Name { get; set; }

        public string Path { get => m_Path; set => SetPath(value); }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { /* Not needed */ }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { Name = System.IO.Path.GetFileName(Path); }

        void SetPath(string path)
        {
            var fileName = PathIO.GetFileName(path);
            Name = fileName;
            m_Path = path;
        }

        /// <summary>
        /// Claims the "Open" action for Matchmaker config assets and routes them to the user's configured
        /// external script editor (Preferences > External Tools), the same IDE Unity opens C# scripts with.
        /// The .mme/.mmq extensions are Unity-specific, so without this handler Unity falls back to
        /// EditorUtility.OpenWithDefaultApp and the OS tries to open the file with whatever it has
        /// associated (e.g. a mail/MIME handler for .mme).
        /// </summary>
        [OnOpenAsset]
#if UNITY_6000_4_OR_NEWER
        static bool OnOpenMatchmakerConfig(EntityId instanceID, int line)
        {
            var obj = EditorUtility.EntityIdToObject(instanceID);
#else
        static bool OnOpenMatchmakerConfig(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
#endif
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (!assetPath.EndsWith(IMatchmakerConfigParser.EnvironmentConfigExtension, StringComparison.OrdinalIgnoreCase)
                && !assetPath.EndsWith(IMatchmakerConfigParser.QueueConfigExtension, StringComparison.OrdinalIgnoreCase))
                return false;

            var editorInstallation = CodeEditor.CodeEditor.CurrentEditorInstallation;
            if (!string.IsNullOrEmpty(editorInstallation))
            {
                var fullPath = PathIO.GetFullPath(assetPath);

                // Use active editor integration from editor preferences.
                if (CodeEditor.CodeEditor.CurrentEditor.OpenProject(fullPath, line))
                    return true;

                var quotedPath = CodeEditor.CodeEditor.QuoteForProcessStart(fullPath);
                if (CodeEditor.CodeEditor.OSOpenFile(editorInstallation, quotedPath))
                    return true;
            }

            // Fall back to custom Inspector if no preferred editor integration is selected.
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            return true;
        }

        [MenuItem("Assets/Create/Services/Matchmaker Environment Config", false, 81)]
        public static void CreateConfigEnv()
        {
            var fileName = $"{k_DefaultFileName}Environment{IMatchmakerConfigParser.EnvironmentConfigExtension}";
            var content = JsonConvert.SerializeObject(EnvironmentConfig.GetDefault(), MatchmakerConfigLoader.GetSerializationSettings());
#if UNITY_6000_4_OR_NEWER
            ProjectWindowUtil.CreateAssetWithTextContent(fileName, content);
#else
            ProjectWindowUtil.CreateAssetWithContent(fileName, content);
#endif
        }

        [MenuItem("Assets/Create/Services/Matchmaker Queue Config", false, 81)]
        public static void CreateConfig()
        {
            var fileName = $"{k_DefaultFileName}Queue{IMatchmakerConfigParser.QueueConfigExtension}";
            var content = JsonConvert.SerializeObject(QueueConfig.GetDefault(), MatchmakerConfigLoader.GetSerializationSettings());
#if UNITY_6000_4_OR_NEWER
            ProjectWindowUtil.CreateAssetWithTextContent(fileName, content);
#else
            ProjectWindowUtil.CreateAssetWithContent(fileName, content);
#endif
        }

        public static void CreateQueueConfig(string queueName, bool focus = false)
        {
            var queueConfig = QueueConfig.GetDefault();
            queueConfig.Name = new QueueName(queueName);

            SaveQueueConfig(queueConfig, focus);
        }

        public static void SaveQueueConfig(QueueConfig config, bool focus = false)
        {
            var filepath = EditorUtility.SaveFilePanelInProject(
                "Queue Config save dialog",
                config.Name.ToString(),
                // extensions are currently defined with a leading . character which would cause issues with the save dialog
                IMatchmakerConfigParser.QueueConfigExtension.TrimStart('.'),
                "Choose a location in your project to save the matchmaker queue configuration.");

            // save was cancelled
            if (filepath.Length == 0)
            {
                return;
            }

            var content = JsonConvert.SerializeObject(config, MatchmakerConfigLoader.GetSerializationSettings());

            File.WriteAllText(filepath, content);
            AssetDatabase.ImportAsset(filepath, ImportAssetOptions.ForceSynchronousImport);

            if (focus)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(filepath);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
    }
}
