using System;

namespace ExamCards.Exam
{
    /// <summary>
    /// Chooses the question source. Editor-only code can set <see cref="Factory"/> to use live data;
    /// anywhere it isn't set, including player builds, questions come from the mock.
    /// </summary>
    public static class QuestionServices
    {
        public static Func<IQuestionService> Factory { get; set; }

        public static IQuestionService Create() => Factory?.Invoke() ?? new MockQuestionService();
    }
}
