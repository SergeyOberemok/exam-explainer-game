using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.Exam.Tests
{
    public class EntityIconResolverTests
    {
        /// <summary><c>Contract/icon_catalog.json</c>: every icon key the backend can send.</summary>
        [Serializable]
        sealed class IconCatalogDto
        {
            public string[] keys;
        }

        static string[] CatalogKeys() =>
            JsonUtility.FromJson<IconCatalogDto>(File.ReadAllText(
                Path.Combine(Application.dataPath, "Tests", "EditMode", "Contract", "icon_catalog.json"))).keys;

        static string Resolve(string icon, EntityKind kind = EntityKind.Object) =>
            EntityIconResolver.Resolve(new VisualEntity { Name = "Thing", Kind = kind, Icon = icon });

        [TestCase("subagent", "subagent")]
        [TestCase(" config-file ", "config-file")]
        [TestCase("undo2", "undo2")]
        [TestCase("", null)]
        [TestCase(null, null)]
        [TestCase("Subagent", null)]
        [TestCase("config_file", null)]
        [TestCase("-file", null)]
        [TestCase("file-", null)]
        [TestCase("config--file", null)]
        [TestCase("../Secrets/key", null)]
        public void CleanKey_KeepsOnlyKeys(string key, string expected) =>
            Assert.AreEqual(expected, EntityIconResolver.CleanKey(key));

        [Test]
        public void Resolve_UsesTheEntitysIcon()
        {
            Assert.AreEqual("subagent", Resolve("subagent", EntityKind.Actor));
            Assert.AreEqual("config-file", Resolve("config-file"));
        }

        [Test]
        public void Resolve_WithoutAnIcon_FallsBackToTheKind()
        {
            Assert.AreEqual(EntityIconResolver.ActorIcon, Resolve(null, EntityKind.Actor));
            Assert.AreEqual(EntityIconResolver.ObjectIcon, Resolve(null));
            Assert.AreEqual(EntityIconResolver.ActionIcon, Resolve("Not A Key", EntityKind.Action));
            Assert.AreEqual(EntityIconResolver.ObjectIcon, EntityIconResolver.Resolve(null));
        }

        [Test]
        public void Catalog_HasTheFallbackKeys()
        {
            var keys = CatalogKeys();
            foreach (var key in new[] { EntityIconResolver.ActorIcon, EntityIconResolver.ObjectIcon, EntityIconResolver.ActionIcon, EntityIconResolver.DefaultIcon })
                CollectionAssert.Contains(keys, key);
        }

        [Test]
        public void EveryCatalogKey_IsAKey_AndShipsAsAVectorImage()
        {
            var keys = CatalogKeys();
            Assert.Greater(keys.Length, 100);
            CollectionAssert.IsEmpty(keys.Where(key => EntityIconResolver.CleanKey(key) != key).ToList(), "Not icon keys.");

            var missing = keys.Where(key => Resources.Load<VectorImage>("EntityIcons/" + key) == null).ToList();
            CollectionAssert.IsEmpty(missing,
                "Run scripts/export_game_icons.py in ../exam-explainer-api to add these to Assets/Resources/EntityIcons/.");
        }

        [Test]
        public void MockQuestions_UseCatalogKeys()
        {
            var keys = CatalogKeys();
            // No latency: awaiting a delay would need the main thread this test is blocking.
            var round = new MockQuestionService(latencyMs: 0).StartRoundAsync().GetAwaiter().GetResult();
            var used = round.Questions
                .Select(question => question.Simplified.Entities)
                .SelectMany(entities => entities.Question.Concat(entities.Answers.SelectMany(answer => answer)).Select(entity => entity.Icon)
                    .Append(entities.QuestionIcon)
                    .Concat(entities.AnswerIcons))
                .Where(icon => icon != null)
                .Distinct()
                .ToList();

            Assert.Greater(used.Count, 5);
            CollectionAssert.IsSubsetOf(used, keys);
        }
    }
}
