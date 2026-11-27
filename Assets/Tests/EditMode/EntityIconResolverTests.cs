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

        static string Resolve(string icon, CastType type = CastType.Object) =>
            EntityIconResolver.Resolve(new CastMember { Name = "Thing", Type = type, Icon = icon });

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
        public void Resolve_UsesTheMembersIcon()
        {
            Assert.AreEqual("subagent", Resolve("subagent", CastType.Actor));
            Assert.AreEqual("config-file", Resolve("config-file"));
        }

        [Test]
        [TestCase(CastType.Actor, EntityIconResolver.ActorIcon)]
        [TestCase(CastType.System, EntityIconResolver.ObjectIcon)]
        [TestCase(CastType.Object, EntityIconResolver.ObjectIcon)]
        [TestCase(CastType.KnowledgeSource, EntityIconResolver.ObjectIcon)]
        [TestCase(CastType.Artifact, EntityIconResolver.ObjectIcon)]
        [TestCase(CastType.ExternalService, EntityIconResolver.ObjectIcon)]
        [TestCase(CastType.Concept, EntityIconResolver.ObjectIcon)]
        public void Resolve_WithoutAnIcon_FallsBackToTheType(CastType type, string expected)
        {
            Assert.AreEqual(expected, Resolve(null, type));
            Assert.AreEqual(expected, Resolve("Not A Key", type));
        }

        [Test]
        public void Resolve_NullOrAnAction_FallsBack()
        {
            Assert.AreEqual(EntityIconResolver.ObjectIcon, EntityIconResolver.Resolve((CastMember)null));
            Assert.AreEqual(EntityIconResolver.ActionIcon, EntityIconResolver.Resolve(new QuestionAction { Name = "Do" }));
            Assert.AreEqual("send", EntityIconResolver.Resolve(new QuestionAction { Name = "Do", Icon = "send" }));
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
                .SelectMany(question => question.Cast.Select(member => member.Icon)
                    .Append(question.Icon)
                    .Concat(question.Tools.Select(tool => tool.Icon)))
                .Where(icon => icon != null)
                .Distinct()
                .ToList();

            Assert.Greater(used.Count, 5);
            CollectionAssert.IsSubsetOf(used, keys);
        }
    }
}
