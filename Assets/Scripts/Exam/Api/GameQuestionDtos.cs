using System;

namespace ExamCards.Exam.Api
{
    // Wire format of the backend's /v2/game routes (pinned by the contract files in Assets/Tests/EditMode/Contract).
    // Public snake_case fields match the JSON keys, because Unity's JsonUtility maps fields by name and ignores
    // properties. The backend sends flat, null-free bodies; a field JsonUtility doesn't find keeps its default.
#pragma warning disable IDE1006 // Naming: these names must match the JSON keys.

    /// <summary>Body of <c>POST /v2/game/rounds</c>.</summary>
    [Serializable]
    public sealed class GameRoundRequestDto
    {
        public int count;
    }

    /// <summary>Response of <c>POST /v2/game/rounds</c>: the cards without their answer key.</summary>
    [Serializable]
    public sealed class GameCardRoundDto
    {
        public string round_id;

        /// <summary>The round's questions, in play order.</summary>
        public GameCardDto[] items;

        /// <summary>Every playable question the round's filters match.</summary>
        public int total;

        public int fifty_fifty_charges;
    }

    /// <summary>
    /// One question as the player meets it before answering: the beat, the briefing item by item, the ask, the
    /// tools (answers) and the map. <c>cast</c> holds every entity the others name by <c>ref</c>.
    /// </summary>
    [Serializable]
    public sealed class GameCardDto
    {
        public string id;
        public string section;
        public string assessment_type;

        /// <summary>Story chapter key; empty for a question without a chapter.</summary>
        public string scenario;

        /// <summary>Key of the chapter's stage; empty until the question has a story record.</summary>
        public string stage;

        /// <summary>Learning-objective keys, the main one first; empty for an untagged question.</summary>
        public string[] objectives;

        /// <summary>1 (easy) to 3 (hard); 0 for an untagged question.</summary>
        public int difficulty;

        /// <summary>Icon key for the question as a whole; empty when none was picked.</summary>
        public string icon;

        /// <summary>One story line to show before the question; empty without a story record.</summary>
        public string beat;

        /// <summary>The full original text.</summary>
        public string question;

        /// <summary><c>question</c> without its scenario paragraph; empty without a chapter.</summary>
        public string stem;

        /// <summary>The question's facts without the ask, in order; empty without a decomposition.</summary>
        public GamePartDto[] briefing;

        public GameAskDto ask;
        public GameCastMemberDto[] cast;
        public GameActionDto[] actions;
        public GameToolDto[] tools;

        /// <summary>The pre-answer flow; empty lists without one.</summary>
        public GameFlowDto map;
    }

    /// <summary>One briefing item; <c>role</c> is context, current_state, problem, consequence, goal or constraint.</summary>
    [Serializable]
    public sealed class GamePartDto
    {
        public string role;
        public string text;

        /// <summary>The <c>ref</c>s of the cast members the item names.</summary>
        public string[] entity_ids;
    }

    /// <summary>The question itself, word for word.</summary>
    [Serializable]
    public sealed class GameAskDto
    {
        public string text;
        public string[] entity_ids;
    }

    /// <summary>
    /// One entity of the question. <c>type</c>: actor, system, object, knowledge_source, artifact, external_service
    /// or concept. <c>scene</c> is false for one only an answer names.
    /// </summary>
    [Serializable]
    public sealed class GameCastMemberDto
    {
        /// <summary>The entity's id (JSON key <c>ref</c>; the <c>@</c> only escapes the keyword).</summary>
        public string @ref;

        public string name;
        public string type;
        public string[] aliases;

        /// <summary>Icon key; empty when the client should draw its default.</summary>
        public string icon;

        public bool scene;
    }

    /// <summary>One event of the scenario: who (<c>actor</c>) does what (<c>name</c>) to what (<c>target</c>).</summary>
    [Serializable]
    public sealed class GameActionDto
    {
        public string @ref;
        public string name;
        public int sequence;
        public string actor;
        public string target;
        public string[] inputs;
        public string[] outputs;
        public string icon;
    }

    /// <summary>One answer as a tool: its text, what it proposes in a few words, and the cast it is made of.</summary>
    [Serializable]
    public sealed class GameToolDto
    {
        public int index;
        public string text;
        public string label;
        public string icon;
        public string[] components;
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

    /// <summary>Body of <c>POST /v2/game/rounds/{round_id}/batches</c>: batch 0 is the start, so the first call asks for 1.</summary>
    [Serializable]
    public sealed class GameRoundBatchRequestDto
    {
        public int batch_index;
        public int count;
    }

    /// <summary>Response of <c>POST /v2/game/rounds/{round_id}/batches</c>: more questions, none already in the round.</summary>
    [Serializable]
    public sealed class GameCardBatchDto
    {
        public int batch_index;

        /// <summary>Empty when the round has used every question.</summary>
        public GameCardDto[] items;

        /// <summary>Playable questions the round doesn't have yet.</summary>
        public int remaining;
    }

    /// <summary>Body of <c>POST /v2/game/rounds/{round_id}/answers</c>.</summary>
    [Serializable]
    public sealed class GameAnswerRequestDto
    {
        public string question_id;
        public int answer_index;
    }

    /// <summary>Response of <c>POST /v2/game/rounds/{round_id}/answers</c> and <c>GET …/answers/{question_id}</c>.</summary>
    [Serializable]
    public sealed class GameCardAnswerResultDto
    {
        public string question_id;
        public int answer_index;
        public bool is_correct;
        public int correct_index;

        /// <summary>What <see cref="answer_index"/> leads to.</summary>
        public GameOutcomeDto outcome;

        /// <summary>Every answer's outcome by index; empty from a backend that sends only <see cref="outcome"/>.</summary>
        public GameOutcomeDto[] outcomes;

        /// <summary>The chapter's story line after this answer (its win or fail line); empty without one.</summary>
        public string story_line;

        public GameReviewDto review;
        public string rule;
        public string[] answer_requirements;

        /// <summary>The map continued with the correct mechanism (nodes and edges marked <c>added</c>).</summary>
        public GameFlowDto resolution_flow;
    }

    /// <summary>What one answer leads to. <c>verdict</c>: correct, incorrect; <c>impact</c>: success, partial, failure.</summary>
    [Serializable]
    public sealed class GameOutcomeDto
    {
        public int index;
        public string verdict;
        public string impact;
        public string consequence;
        public string misconception;
        public string reason;
        public string[] gaps;
        public string[] supported_requirements;
    }

    /// <summary>Feedback per answer and the explanation of an answered question.</summary>
    [Serializable]
    public sealed class GameReviewDto
    {
        public string explanation;
        public string[] feedbacks;
    }

    /// <summary>Body of <c>POST /v2/game/rounds/{round_id}/fifty-fifty</c>.</summary>
    [Serializable]
    public sealed class GameFiftyFiftyRequestDto
    {
        public string question_id;
    }

    /// <summary>Response of <c>POST /v2/game/rounds/{round_id}/fifty-fifty</c>.</summary>
    [Serializable]
    public sealed class GameFiftyFiftyResultDto
    {
        public string question_id;
        public int[] eliminated_indices;
        public int fifty_fifty_remaining;
    }

    /// <summary>Body of <c>POST /v2/game/rounds/{round_id}/questions</c>.</summary>
    [Serializable]
    public sealed class GameRoundQuestionRequestDto
    {
        public string question_id;
    }

    /// <summary>Response of <c>POST /v2/game/rounds/{round_id}/questions</c>: the added question, without its key.</summary>
    [Serializable]
    public sealed class GameCardRoundQuestionDto
    {
        public GameCardDto item;
    }

    /// <summary>
    /// Body of <c>POST /v2/search/game-questions</c>. A body rather than the GET's query string, because a pasted
    /// question, percent-encoded, can pass the URL limit of a server or proxy.
    /// </summary>
    [Serializable]
    public sealed class GameSearchRequestDto
    {
        public string q;
        public int limit;
    }

    /// <summary>Response of <c>POST /v2/search/game-questions</c>: playable matches, best first, without their key.</summary>
    [Serializable]
    public sealed class GameCardListDto
    {
        public GameCardDto[] items;
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
