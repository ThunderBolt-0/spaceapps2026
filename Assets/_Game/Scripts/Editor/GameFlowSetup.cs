using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MissionTrainer.EditorTools
{
    /// <summary>
    /// Menu: Mission Trainer > Add Menus to Mars Scene.
    /// Adds the title screen / chapter map / science page flow (GameFlow) to the existing Mars scene,
    /// without regenerating terrain or art. Safe to run more than once.
    /// </summary>
    public static class GameFlowSetup
    {
        const string ScenePath = "Assets/_Game/Scenes/Mars.unity";

        [MenuItem("Mission Trainer/Add Menus to Mars Scene")]
        public static void AddToMars()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("No Mars scene", "Run Mission Trainer > Build Mars Scene (3D) first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Attach();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Mars scene now starts on the title screen. Press Play.");
        }

        /// <summary>Adds/wires GameFlow on the object that has the MissionRunner. Also used by the scene builder.</summary>
        public static GameFlow Attach()
        {
            var runner = Object.FindAnyObjectByType<MissionRunner>();
            if (!runner) { Debug.LogError("No MissionRunner in the open scene."); return null; }
            runner.autoStart = false;
            var flow = runner.GetComponent<GameFlow>();
            if (!flow) flow = runner.gameObject.AddComponent<GameFlow>();
            flow.runner = runner;
            flow.hud = runner.GetComponent<PlaytestUI>();
            flow.player = Object.FindAnyObjectByType<FirstPersonController>();
            if (flow.player) flow.playerCamera = flow.player.GetComponentInChildren<Camera>(true);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(flow);
            return flow;
        }
    }
}
