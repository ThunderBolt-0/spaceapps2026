using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MissionTrainer.EditorTools
{
    /// <summary>Menu: Mission Trainer > Open Playtest Scene. Creates the scene the first time, then just opens it.</summary>
    public static class PlaytestSceneMenu
    {
        const string ScenePath = "Assets/_Game/Scenes/Playtest.unity";

        [MenuItem("Mission Trainer/Open Playtest Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.13f);

            var go = new GameObject("Playtest");
            var runner = go.AddComponent<MissionRunner>();
            runner.fallbackMissionId = "test_mission";
            go.AddComponent<PlaytestUI>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("Created " + ScenePath + ". Press Play to try the test mission.");
        }
    }
}
