using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;

namespace MCPForUnityTests.Editor
{
    /// <summary>
    /// Protects per-scene change baselines from unrelated scene-open/save events.
    /// </summary>
    public class SceneExternalChangeGuardTests
    {
        private string _tempDirectory;
        private string _baselineKey;
        private string _previousBaseline;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "McpSceneGuard_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);

            var field = typeof(MCPForUnity.Editor.Helpers.SceneExternalChangeGuard).GetField(
                "BaselineKey", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field);
            _baselineKey = (string)field.GetValue(null);
            _previousBaseline = SessionState.GetString(_baselineKey, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            SessionState.SetString(_baselineKey, _previousBaseline ?? string.Empty);
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
        }

        [Test]
        public void KeepEditorSaveFailureBlocksRefreshAndDoesNotClaimOverwrite()
        {
            var outcome = new MCPForUnity.Editor.Helpers.SceneExternalChangeGuard.Outcome();
            bool saved = MCPForUnity.Editor.Helpers.SceneExternalChangeGuard.RecordOverwriteResult(
                outcome, "Assets/Scenes/Changed.unity", false);
            Assert.IsFalse(saved);
            Assert.IsTrue(outcome.Blocked);
            StringAssert.Contains("Failed to save", outcome.Error);
            Assert.IsEmpty(outcome.OverwrittenScenes);
        }

        [Test]
        public void KeepEditorSuccessfulSaveIsRecordedAsOverwritten()
        {
            var outcome = new MCPForUnity.Editor.Helpers.SceneExternalChangeGuard.Outcome();
            bool saved = MCPForUnity.Editor.Helpers.SceneExternalChangeGuard.RecordOverwriteResult(
                outcome, "Assets/Scenes/Changed.unity", true);
            Assert.IsTrue(saved);
            Assert.IsFalse(outcome.Blocked);
            CollectionAssert.AreEqual(new[] { "Assets/Scenes/Changed.unity" }, outcome.OverwrittenScenes);
        }

        [TestCase("invalid")]
        [TestCase("discard_everything")]
        public void Reconcile_UnknownModeFailsClosed(string mode)
        {
            var outcome = MCPForUnity.Editor.Helpers.SceneExternalChangeGuard.Reconcile(mode);
            Assert.IsTrue(outcome.Blocked);
            StringAssert.Contains("Unsupported on_external_scene_change mode", outcome.Error);
            StringAssert.Contains("auto", outcome.Error);
            StringAssert.Contains("reload", outcome.Error);
            StringAssert.Contains("keep_editor", outcome.Error);
        }

        [Test]
        public void UpdatingOneSceneBaselinePreservesAnUnreconciledSceneBaseline()
        {
            string changedScenePath = Path.Combine(_tempDirectory, "SceneA.unity");
            string openedScenePath = Path.Combine(_tempDirectory, "SceneB.unity");
            File.WriteAllText(changedScenePath, "external edit");
            File.WriteAllText(openedScenePath, "new scene");

            long originalBaseline = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            SessionState.SetString(
                _baselineKey,
                JsonConvert.SerializeObject(new Dictionary<string, long>
                {
                    [changedScenePath] = originalBaseline
                }));

            var upsert = typeof(MCPForUnity.Editor.Helpers.SceneExternalChangeGuard).GetMethod(
                "UpsertBaseline", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(upsert);
            upsert.Invoke(null, new object[] { openedScenePath });

            var baselines = JsonConvert.DeserializeObject<Dictionary<string, long>>(
                SessionState.GetString(_baselineKey, string.Empty));

            Assert.IsNotNull(baselines);
            Assert.AreEqual(
                originalBaseline,
                baselines[changedScenePath],
                "Opening or saving Scene B must not reset Scene A's externally changed timestamp.");
            Assert.AreEqual(File.GetLastWriteTimeUtc(openedScenePath).Ticks, baselines[openedScenePath]);
        }
    }
}
