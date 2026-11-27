using NUnit.Framework;

namespace ExamCards.Exam.Tests
{
    public class TypewriterSequenceTests
    {
        // 10 chars per second, no pauses: "abcde" takes 0.5 s.
        static readonly TypewriterOptions Plain = new(10f, 100f, 0f);
        const float Gap = 0.2f;

        static TypewriterSequence.Position At(string[] texts, float t) => TypewriterSequence.At(texts, t, Plain, Gap);

        [Test]
        public void At_TypesEachTextInTurn_WithAGapBetween()
        {
            var texts = new[] { "abcde", "xyz" };

            var start = At(texts, 0f);
            Assert.AreEqual(0, start.Index);
            Assert.AreEqual(0, start.Length);

            var first = At(texts, 0.25f);
            Assert.AreEqual(0, first.Index);
            Assert.AreEqual(2, first.Length);

            // In the gap the first text shows whole and the second hasn't started.
            var gap = At(texts, 0.6f);
            Assert.AreEqual(0, gap.Index);
            Assert.AreEqual(5, gap.Length);

            var second = At(texts, 0.5f + Gap + 0.15f);
            Assert.AreEqual(1, second.Index);
            Assert.AreEqual(1, second.Length);
        }

        [Test]
        public void At_AfterTheTotal_IsDone()
        {
            var texts = new[] { "abcde", "xyz" };
            var total = TypewriterSequence.TotalDuration(texts, Plain, Gap);

            Assert.AreEqual(0.5f + Gap + 0.3f, total, 1e-4f);
            Assert.AreEqual(2, At(texts, total).Index);
            Assert.AreEqual(2, At(texts, total + 5f).Index);
        }

        [Test]
        public void At_SkipsEmptyTexts_AndTheirGaps()
        {
            var texts = new[] { null, "abc", "", "xy" };

            Assert.AreEqual(1, At(texts, 0f).Index);
            Assert.AreEqual(0.3f + Gap + 0.2f, TypewriterSequence.TotalDuration(texts, Plain, Gap), 1e-4f);
            var last = At(texts, 0.3f + Gap + 0.15f);
            Assert.AreEqual(3, last.Index);
            Assert.AreEqual(1, last.Length);
            Assert.AreEqual(2, At(new[] { "", null }, 0f).Index, "Nothing to type is done at once.");
        }

        [Test]
        public void At_CapsEachTextOnItsOwn()
        {
            var capped = new TypewriterOptions(10f, 1f, 0f);
            var texts = new[] { new string('a', 100), "xy" };

            Assert.AreEqual(1f + Gap + 0.2f, TypewriterSequence.TotalDuration(texts, capped, Gap), 1e-4f);
            Assert.AreEqual(1, TypewriterSequence.At(texts, 1f + Gap + 0.05f, capped, Gap).Index);
        }

        [Test]
        public void At_NoTexts_IsDone()
        {
            Assert.AreEqual(0, TypewriterSequence.At(null, 0f, Plain, Gap).Index);
            Assert.AreEqual(0f, TypewriterSequence.TotalDuration(null, Plain, Gap));
        }
    }
}
