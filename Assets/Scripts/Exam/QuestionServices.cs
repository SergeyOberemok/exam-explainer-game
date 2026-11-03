using System;

namespace ExamCards.Exam
{
    /// <summary>
    /// Chooses the question source. The API assembly sets <see cref="Factory"/> before the first scene loads;
    /// where it isn't set, such as in EditMode tests, questions come from the mock.
    /// </summary>
    public static class QuestionServices
    {
        public static Func<IQuestionService> Factory { get; set; }

        public static IQuestionService Create() => Factory?.Invoke() ?? new MockQuestionService();
    }
}
