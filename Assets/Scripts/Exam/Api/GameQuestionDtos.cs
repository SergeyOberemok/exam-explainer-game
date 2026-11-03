using System;

namespace ExamCards.Exam.Api
{
    // Wire format of the backend's /game routes. Public snake_case fields match the JSON keys,
    // because Unity's JsonUtility maps fields by name and ignores properties.
#pragma warning disable IDE1006 // Naming: these names must match the JSON keys.

    /// <summary>Body of <c>POST /game/rounds</c>.</summary>
    [Serializable]
    public sealed class GameRoundRequestDto
    {
        public int count;
    }

    /// <summary>Response of <c>POST /game/rounds</c>: the questions without their answer key.</summary>
    [Serializable]
    public sealed class GameRoundDto
    {
        public string round_id;

        /// <summary>The round's questions, in play order.</summary>
        public GameQuestionDto[] items;

        /// <summary>Simplified versions of the questions that have one, matched by <c>id</c>.</summary>
        public GameQuestionDto[] simplified;

        public int total;
        public int fifty_fifty_charges;
    }

    /// <summary>One version of a question, as a player may see it before answering.</summary>
    [Serializable]
    public sealed class GameQuestionDto
    {
        public string id;
        public bool simplified;
        public string section;
        public string assessment_type;
        public string question;
        public GameAnswerDto[] answers;
    }

    /// <summary>One answer option of a <see cref="GameQuestionDto"/>.</summary>
    [Serializable]
    public sealed class GameAnswerDto
    {
        public int index;
        public string text;
    }

    /// <summary>Body of <c>POST /game/rounds/{round_id}/answers</c>.</summary>
    [Serializable]
    public sealed class GameAnswerRequestDto
    {
        public string question_id;
        public int answer_index;
    }

    /// <summary>Response of <c>POST /game/rounds/{round_id}/answers</c>.</summary>
    [Serializable]
    public sealed class GameAnswerResultDto
    {
        public string question_id;
        public int answer_index;
        public bool is_correct;
        public int correct_index;
        public GameReviewDto original;
        public bool has_simplified;

        /// <summary>Empty when <see cref="has_simplified"/> is false.</summary>
        public GameReviewDto simplified;
    }

    /// <summary>Feedback and explanation of one version of an answered question.</summary>
    [Serializable]
    public sealed class GameReviewDto
    {
        public string explanation;
        public string[] feedbacks;
    }

    /// <summary>Body of <c>POST /game/rounds/{round_id}/fifty-fifty</c>.</summary>
    [Serializable]
    public sealed class GameFiftyFiftyRequestDto
    {
        public string question_id;
    }

    /// <summary>Response of <c>POST /game/rounds/{round_id}/fifty-fifty</c>.</summary>
    [Serializable]
    public sealed class GameFiftyFiftyResultDto
    {
        public string question_id;
        public int[] eliminated_indices;
        public int fifty_fifty_remaining;
    }

    /// <summary>FastAPI's error body when <c>detail</c> is a message (validation errors use a list instead).</summary>
    [Serializable]
    public sealed class ApiErrorDto
    {
        public string detail;
    }

#pragma warning restore IDE1006
}
