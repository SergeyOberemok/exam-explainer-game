using ExamCards.Exam.Api;
using ExamCards.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.Editor
{
    /// <summary>One-click setup of the PickOne screen in SampleScene (PanelSettings + UIDocument + controller + API settings).</summary>
    public static class PickOneSceneSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string PanelSettingsPath = "Assets/UI/PanelSettings.asset";
        const string ThemePath = "Assets/UI/ExamTheme.tss";
        const string ScreenUxmlPath = "Assets/UI/PickOne/PickOne.uxml";
        const string CardUxmlPath = "Assets/UI/PickOne/AnswerCard.uxml";
        const string GameObjectName = "PickOneUI";
        const string ApiSettingsPath = "Assets/Resources/" + ExamApiSettings.ResourceName + ".asset";

        [MenuItem("Tools/ExamCards/Setup PickOne Scene")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var panelSettings = GetOrCreatePanelSettings();
            GetOrCreateApiSettings();
            var screenUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ScreenUxmlPath);
            var cardUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CardUxmlPath);
            if (screenUxml == null || cardUxml == null)
            {
                Debug.LogError($"Missing UXML at {ScreenUxmlPath} or {CardUxmlPath}.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find(GameObjectName);
            if (go == null)
                go = new GameObject(GameObjectName);
            if (!go.TryGetComponent(out UIDocument document))
                document = go.AddComponent<UIDocument>();
            // Assign through SerializedObject: the UIDocument property setters don't reliably persist into the saved scene.
            var serializedDocument = new SerializedObject(document);
            serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
            serializedDocument.FindProperty("sourceAsset").objectReferenceValue = screenUxml;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();

            if (!go.TryGetComponent(out PickOneScreen screen))
                screen = go.AddComponent<PickOneScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("_answerCardTemplate").objectReferenceValue = cardUxml;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;
            Debug.Log($"PickOne screen set up in {ScenePath}.");
        }

        static ExamApiSettings GetOrCreateApiSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ExamApiSettings>(ApiSettingsPath);
            if (settings != null)
                return settings;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            settings = ScriptableObject.CreateInstance<ExamApiSettings>();
            AssetDatabase.CreateAsset(settings, ApiSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {ApiSettingsPath}; set the backend URL there.");
            return settings;
        }

        static PanelSettings GetOrCreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 1024);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
