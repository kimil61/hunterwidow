using HunterWidow.Unity.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HunterWidow.Editor
{
    /// <summary>
    /// Keeps the shipped scene explicitly authored as a 2D entry scene rather than
    /// relying on a runtime-only bootstrap object.
    /// </summary>
    public static class HunterWidowSceneSetup
    {
        private const string MvpScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Hunter Widow/Ensure MVP 2D Scene")]
        public static void EnsureMvpScene()
        {
            var scene = EditorSceneManager.OpenScene(MvpScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindFirstObjectByType<HunterWidowBootstrap>();
            if (bootstrap == null)
            {
                var bootstrapObject = new GameObject("HunterWidowBootstrap");
                bootstrapObject.AddComponent<HunterWidowBootstrap>();
            }

            var authoredRoot = GameObject.Find("HunterWidowMvp2DScene");
            if (authoredRoot == null)
            {
                authoredRoot = new GameObject("HunterWidowMvp2DScene");
                authoredRoot.transform.position = Vector3.zero;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
