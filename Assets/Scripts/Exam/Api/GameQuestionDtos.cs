using System;

namespace ExamCards.Exam.Api
{
    // Wire format of the backend's /v1/game routes (pinned by the contract files in Assets/Tests/EditMode/Contract). Public snake_case fields match the JSON keys,
    // because Unity's JsonUtility maps fields by name and ignores properties.
#pragma warning disable IDE1006 // Naming: these names must match the JSON keys.

    /// <summary>Body of <c>POST /v1/game/rounds</c>.</summary>
    [Serializable]
    public sealed class GameRoundRequestDto
    {
        public int count;
    }

    /// <summary>Response of <c>POST /v1/game/rounds</c>: the questions without their answer key.</summary>
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

    /// <summary>Body of <c>POST /v1/game/rounds/{round_id}/batches</c>: batch 0 is the start, so the first call asks for 1.</summary>
    [Serializable]
    public sealed class GameRoundBatchRequestDto
    {
        public int batch_index;
        public int count;
    }

    /// <summary>Response of <c>POST /v1/game/rounds/{round_id}/batches</c>: more questions, none already in the round.</summary>
    [Serializable]
    public sealed class GameRoundBatchDto
    {
        public int batch_index;

        /// <summary>Empty when the round has used every question.</summary>
        public GameQuestionDto[] items;

        /// <summary>Simplified versions of the items that have one, matched by <c>id</c>.</summary>
        public GameQuestionDto[] simplified;

        /// <summary>Playable questions the round doesn't have yet.</summary>
        public int remaining;
    }

    /// <summary>Body of <c>POST /v1/game/rounds/{round_id}/answers</c>.</summary>
    [Serializable]
    public sealed class GameAnswerRequestDto
    {
        public string question_id;
        public int answer_index;
    }

    /// <summary>Response of <c>POST /v1/game/rounds/{round_id}/answers</c> and <c>GET /v1/game/rounds/{round_id}/answers/{question_id}</c>.</summary>
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

    /// <summary>Body of <c>POST /v1/game/rounds/{round_id}/fifty-fifty</c>.</summary>
    [Serializable]
    public sealed class GameFiftyFiftyRequestDto
    {
        public string question_id;
    }

    /// <summary>Response of <c>POST /v1/game/rounds/{round_id}/fifty-fifty</c>.</summary>
    [Serializable]
    public sealed class GameFiftyFiftyResultDto
    {
        public string question_id;
        public int[] eliminated_indices;
        public int fifty_fifty_remaining;
    }

    /// <summary>Body of <c>POST /v1/game/rounds/{round_id}/questions</c>.</summary>
    [Serializable]
    public sealed class GameRoundQuestionRequestDto
    {
        public string question_id;
    }

    /// <summary>Response of <c>POST /v1/game/rounds/{round_id}/questions</c>: the added question, without its key.</summary>
    [Serializable]
    public sealed class GameRoundQuestionDto
    {
        public GameQuestionDto item;

        /// <summary>Its simplified version: one item, or none.</summary>
        public GameQuestionDto[] simplified;
    }

    /// <summary>Response of <c>GET /v1/search/game-questions</c>: playable matches, best first, without their key.</summary>
    [Serializable]
    public sealed class GameQuestionListDto
    {
        public GameQuestionDto[] items;
        public int total;
    }

    /// <summary>The backend's error body: a message and a machine-readable <c>code</c> such as <c>already_answered</c>.</summary>
    [Serializable]
    public sealed class ApiErrorDto
    {
        public string detail;
        public string code;
    }

#pragma warning restore IDE1006
}
