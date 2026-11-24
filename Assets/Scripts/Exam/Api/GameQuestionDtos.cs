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

        /// <summary>Visual entities; empty lists for originals (and null when the field is missing).</summary>
        public GameEntitiesDto entities;

        /// <summary>Learning-objective keys, the main one first; empty (or null when missing) for an untagged question.</summary>
        public string[] objectives;

        /// <summary>1 (easy) to 3 (hard); 0 for an untagged question.</summary>
        public int difficulty;

        /// <summary>Story chapter key; empty (or null when missing) for a question without a chapter.</summary>
        public string scenario;

        /// <summary>Key of the chapter's stage; empty until the question has a story record.</summary>
        public string stage;

        /// <summary><c>question</c> without its scenario paragraph; empty without a chapter and on simplified versions.</summary>
        public string stem;

        /// <summary>One story line to show before the question; empty until the question has a story record.</summary>
        public string beat;
    }

    /// <summary>The visual entities of a <see cref="GameQuestionDto"/>; <c>answers</c> is index-aligned with its answers.</summary>
    [Serializable]
    public sealed class GameEntitiesDto
    {
        /// <summary>Icon key for the question as a whole; null or empty when none was picked.</summary>
        public string question_icon;
        public GameEntityDto[] question;
        public GameAnswerEntitiesDto[] answers;
    }

    /// <summary>The entities of one answer (an object, because JsonUtility can't read lists of lists).</summary>
    [Serializable]
    public sealed class GameAnswerEntitiesDto
    {
        public GameEntityDto[] entities;

        /// <summary>Icon key for what the answer proposes; null or empty when none was picked.</summary>
        public string icon;
    }

    /// <summary>One visual entity; <c>kind</c> is "actor", "object" or "action", <c>icon</c> a key of the backend's icon catalog.</summary>
    [Serializable]
    public sealed class GameEntityDto
    {
        public string name;
        public string kind;
        public string[] aliases;
        public string icon;
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

        /// <summary>What <see cref="answer_index"/> suggests the player got wrong; empty for the correct answer or an untagged question.</summary>
        public string misconception;

        /// <summary>What the answer leads to in the story; empty for a question without a story record.</summary>
        public string outcome;
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

    /// <summary>
    /// Body of <c>POST /v1/search/game-questions</c>. A body rather than the GET's query string, because a pasted
    /// question, percent-encoded, can pass the URL limit of a server or proxy.
    /// </summary>
    [Serializable]
    public sealed class GameSearchRequestDto
    {
        public string q;
        public int limit;
    }

    /// <summary>Response of <c>POST /v1/search/game-questions</c>: playable matches, best first, without their key.</summary>
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
