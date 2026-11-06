using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.Exam.Tests
{
    public class EntityIconResolverTests
    {
        static string Resolve(string name, EntityKind kind = EntityKind.Object, params string[] aliases) =>
            EntityIconResolver.Resolve(new VisualEntity { Name = name, Kind = kind, Aliases = aliases });

        [TestCase("`stop_reason`", "stop reason")]
        [TestCase("acceptEdits", "accept edits")]
        [TestCase("  MCP-Server ", "mcp server")]
        [TestCase("git reset --hard", "git reset hard")]
        [TestCase("CLAUDE.md", "claude md")]
        [TestCase("", "")]
        public void Normalize_SplitsWordsAndCamelCase(string text, string expected) =>
            Assert.AreEqual(expected, EntityIconResolver.Normalize(text));

        [Test]
        public void ExactNames_WinOverKeywords()
        {
            Assert.AreEqual("square-terminal", Resolve("Claude Code"));
            Assert.AreEqual("map", Resolve("plan mode"));
            Assert.AreEqual("brain-circuit", Resolve("Main Agent", EntityKind.Actor));
            Assert.AreEqual("server", Resolve("Weather", EntityKind.Object, "MCP server"));
        }

        [Test]
        public void Keywords_ReadTheHeadNoun_OrTheVerbOfAnAction()
        {
            Assert.AreEqual("key-round", Resolve("x-api-key"));
            Assert.AreEqual("database", Resolve("Customer Database"));
            Assert.AreEqual("user", Resolve("Customer", EntityKind.Actor));
            Assert.AreEqual("search", Resolve("Lookup Order", EntityKind.Action));
            Assert.AreEqual("package", Resolve("Lookup Order", EntityKind.Object));
            Assert.AreEqual("wrench", Resolve("Tools"));
            Assert.AreEqual("bot", Resolve("research agents", EntityKind.Actor));
        }

        [Test]
        public void Unknown_FallsBackToTheKind()
        {
            Assert.AreEqual(EntityIconResolver.ActorIcon, Resolve("Zorblax", EntityKind.Actor));
            Assert.AreEqual(EntityIconResolver.ObjectIcon, Resolve("Zorblax"));
            Assert.AreEqual(EntityIconResolver.ActionIcon, Resolve("Zorblax", EntityKind.Action));
            Assert.AreEqual(EntityIconResolver.ObjectIcon, EntityIconResolver.Resolve(null));
        }

        [Test]
        public void EveryKnownIcon_ShipsAsAVectorImage()
        {
            var missing = EntityIconResolver.KnownIcons
                .Where(name => Resources.Load<VectorImage>("EntityIcons/" + name) == null)
                .ToList();
            CollectionAssert.IsEmpty(missing, "Add these to Assets/Resources/EntityIcons/ (as VectorImage).");
        }
    }
}
