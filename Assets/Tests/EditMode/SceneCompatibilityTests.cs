using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneCompatibilityTests
{
    [Test]
    public void AllBuildScenesKeepScriptAndBattleBindingsAfterAssemblySplit()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Assert.That(scenes.Length, Is.EqualTo(8));
        foreach (var entry in scenes)
        {
            // Edit-mode only: no Start/Awake game loop and no access to player saves.
            var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(entry.path);
            var openedHere = !existing.IsValid() || !existing.isLoaded;
            var scene = openedHere ? EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive) : existing;
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero,
                            entry.path + ": " + transform.name);
                if (scene.name != "BattleScene") continue;
                var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSceneManager>(true)).Single();
                Assert.That(manager.unitPrefab, Is.Not.Null);
                Assert.That(manager.playerSquadPanel, Is.Not.Null);
                Assert.That(manager.enemySquadPanel, Is.Not.Null);
                Assert.That(manager.abilityPanel, Is.Not.Null);
                Assert.That(manager.fightButton, Is.Not.Null);
                Assert.That(manager.winButton, Is.Not.Null);
                Assert.That(manager.loseButton, Is.Not.Null);
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
