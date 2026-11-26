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

        /// <summary>Visual entities, the same for both versions; empty lists without a decomposition (and null when the field is missing).</summary>
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

        /// <summary>The question as facts and its ask; originals only, empty without a decomposition.</summary>
        public GamePartDto[] parts;

        /// <summary>The pre-answer flow; empty lists without one.</summary>
        public GameFlowDto flow;
    }

    /// <summary>One piece of a question; <c>role</c> is context, current_state, problem, consequence, goal, constraint or ask.</summary>
    [Serializable]
    public sealed class GamePartDto
    {
        public string role;
        public string text;

        /// <summary>The <c>ref</c>s of the entities the part names.</summary>
        public string[] entity_ids;
    }

    /// <summary>A flow chart, flat and null-free; every list is empty when the question has none.</summary>
    [Serializable]
    public sealed class GameFlowDto
    {
        public string primary;
        public string[] secondary;
        public string reason;
        public string trigger;
        public FlowNodeDto[] nodes;
        public FlowEdgeDto[] edges;
    }

    /// <summary><c>type</c>: start, process, decision, input, output, human_review, end; <c>source_status</c>: explicit, inferred, unknown.</summary>
    [Serializable]
    public sealed class FlowNodeDto
    {
        public string id;
        public string type;
        public string label;
        public string[] entity_ids;
        public string condition_id;
        public string source_status;
        public bool is_question;
        public bool added;
    }

    /// <summary><c>type</c>: next, yes, no, unknown, approved, rejected, custom.</summary>
    [Serializable]
    public sealed class FlowEdgeDto
    {
        public string source;
        public string target;
        public string type;
        public string label;
        public string source_status;
        public bool added;
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

        /// <summary>The entity's decomposition id (JSON key <c>ref</c>; the <c>@</c> only escapes the keyword).</summary>
        public string @ref;
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
        /// <summary>The only review: the simplified text has none of its own.</summary>
        public GameReviewDto original;

        /// <summary>What <see cref="answer_index"/> suggests the player got wrong; empty for the correct answer or an untagged question.</summary>
        public string misconception;

        /// <summary>What the answer leads to in the story; empty for a question without a story record.</summary>
        public string outcome;

        /// <summary>Why the correct answer is correct; empty lists and strings when the backend has none.</summary>
        public GameResolutionDto resolution;
    }

    [Serializable]
    public sealed class GameResolutionDto
    {
        public string[] answer_requirements;

        /// <summary>The pre-answer flow without its question node, continued with steps marked <c>added</c>.</summary>
        public GameFlowDto flow;

        public OptionEvaluationDto[] options;
        public string rule;
    }

    /// <summary>One answer compared with the requirements; <c>verdict</c> is "correct" or "incorrect".</summary>
    [Serializable]
    public sealed class OptionEvaluationDto
    {
        public int index;
        public string verdict;
        public string[] supported_requirements;
        public string[] gaps;
        public string reason;
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
