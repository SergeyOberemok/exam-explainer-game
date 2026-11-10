using ExamCards.Exam;
using NUnit.Framework;

namespace ExamCards.Tests
{
    public class TypewriterTimelineTests
    {
        static readonly TypewriterOptions Plain = new(10f, 100f, 0f);

        [Test]
        public void NothingIsVisibleAtStart()
        {
            Assert.AreEqual(0, TypewriterTimeline.VisibleLength("hello", 0f, Plain));
        }

        [Test]
        public void FullTextAfterTheDuration()
        {
            Assert.AreEqual(5, TypewriterTimeline.VisibleLength("hello", 10f, Plain));
            Assert.AreEqual(0.5f, TypewriterTimeline.TotalDuration("hello", Plain), 1e-4f);
        }

        [Test]
        public void GrowsWithTime()
        {
            var last = 0;
            for (var t = 0f; t <= 0.6f; t += 0.05f)
            {
                var n = TypewriterTimeline.VisibleLength("hello", t, Plain);
                Assert.GreaterOrEqual(n, last);
                last = n;
            }
            Assert.AreEqual(2, TypewriterTimeline.VisibleLength("hello", 0.25f, Plain));
        }

        [Test]
        public void LongTextIsCappedAtMaxDuration()
        {
            var text = new string('a', 1000);
            var options = new TypewriterOptions(10f, 2f, 0f);
            Assert.AreEqual(2f, TypewriterTimeline.TotalDuration(text, options), 1e-4f);
            Assert.AreEqual(1000, TypewriterTimeline.VisibleLength(text, 2f, options));
            Assert.AreEqual(500, TypewriterTimeline.VisibleLength(text, 1f, options), 2);
        }

        [Test]
        public void PauseDelaysTheNextCharacters()
        {
            var options = new TypewriterOptions(10f, 100f, 1f);
            Assert.AreEqual(2, TypewriterTimeline.VisibleLength("a.b", 0.5f, options));
            Assert.AreEqual(2, TypewriterTimeline.VisibleLength("a.b", 1.0f, options));
            Assert.AreEqual(3, TypewriterTimeline.VisibleLength("a.b", 1.3f, options));
        }

        [Test]
        public void NeverSplitsASurrogatePair()
        {
            const string text = "a\U0001F600b";
            for (var t = 0f; t <= 0.5f; t += 0.01f)
            {
                var n = TypewriterTimeline.VisibleLength(text, t, Plain);
                Assert.IsFalse(n > 0 && n < text.Length && char.IsHighSurrogate(text[n - 1]), $"split at {n}");
            }
        }

        [Test]
        public void EmptyTextHasNothingToShow()
        {
            Assert.AreEqual(0, TypewriterTimeline.VisibleLength("", 1f, Plain));
            Assert.AreEqual(0, TypewriterTimeline.VisibleLength(null, 1f, Plain));
            Assert.AreEqual(0f, TypewriterTimeline.TotalDuration("", Plain));
        }
    }
}
