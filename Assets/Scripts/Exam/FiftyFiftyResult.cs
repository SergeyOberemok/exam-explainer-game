using System.Collections.Generic;

namespace ExamCards.Exam
{
    /// <summary>What a 50/50 removed, and how many uses the round has left (the round's count is the one that counts).</summary>
    public sealed class FiftyFiftyResult
    {
        public FiftyFiftyResult(IReadOnlyList<int> eliminated, int remaining)
        {
            Eliminated = eliminated;
            Remaining = remaining;
        }

        /// <summary>The indices of the removed answers.</summary>
        public IReadOnlyList<int> Eliminated { get; }

        /// <summary>50/50 uses left in the round after this one.</summary>
        public int Remaining { get; }
    }
}
