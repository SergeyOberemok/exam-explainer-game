using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// Stand-in for live data, used by tests and whenever <see cref="QuestionServices.Factory"/> isn't set:
    /// starts a <see cref="LocalExamRound"/> over a fixed set of questions after a short fake latency.
    /// Search looks through those and a few more, which can be added to the round.
    /// The questions have the shape of the backend's composed cards: a briefing, the ask, a cast, the answers as
    /// tools, and one outcome per answer.
    /// </summary>
    public sealed class MockQuestionService : IQuestionService, IQuestionSearch
    {
        /// <summary>The chapter every sample question belongs to.</summary>
        public const string SampleScenario = "sample-service";

        readonly int _latencyMs;

        public MockQuestionService(int latencyMs = 300)
        {
            _latencyMs = latencyMs;
        }

        public async Task<IExamRound> StartRoundAsync(CancellationToken cancellationToken = default)
        {
            if (_latencyMs > 0)
                await Task.Delay(_latencyMs, cancellationToken);
            return new LocalExamRound(BuildQuestions(), catalog: BuildCatalog());
        }

        /// <summary>Case-insensitive match on the question, its answers or its section, like the backend's.</summary>
        public async Task<IReadOnlyList<Question>> SearchAsync(string text, CancellationToken cancellationToken = default)
        {
            text = QuestionSearchText.Normalize(text)
                ?? throw new ArgumentException($"Search for at least {IQuestionSearch.MinTextLength} characters.", nameof(text));
            if (_latencyMs > 0)
                await Task.Delay(_latencyMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            bool Matches(string value) => value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
            // Like the backend's search results: whole cards, without their key.
            return BuildCatalog()
                .Select(keyed => keyed.Question)
                .Where(q => Matches(q.Text) || Matches(q.Section) || q.Answers.Any(Matches))
                .ToList();
        }

        // ---------- builders ----------

        static CastMember Member(string reference, string name, CastType type, string icon, bool scene = true) =>
            new CastMember { Ref = reference, Name = name, Type = type, Icon = icon, InScene = scene };

        static QuestionPart Part(PartRole role, string text, params string[] refs) =>
            new QuestionPart { Role = role, Text = text, EntityRefs = refs };

        static AnswerTool Tool(int index, string text, string label, string icon, params string[] components) =>
            new AnswerTool { Index = index, Text = text, Label = label, Icon = icon, ComponentRefs = components };

        static AnswerOutcome Outcome(int index, bool correct, AnswerImpact impact, string consequence, string reason,
            string misconception = "", string[] meets = null, string[] gaps = null) =>
            new AnswerOutcome
            {
                Index = index,
                IsCorrect = correct,
                Impact = impact,
                Consequence = consequence,
                Reason = reason,
                Misconception = misconception,
                SupportedRequirements = meets ?? Array.Empty<string>(),
                Gaps = gaps ?? Array.Empty<string>(),
            };

        static FlowNode Node(string id, FlowNodeType type, string label, bool isQuestion = false, bool added = false,
            string[] refs = null) =>
            new FlowNode
            {
                Id = id, Type = type, Label = label, Status = FlowStatus.Explicit, IsQuestion = isQuestion, Added = added,
                EntityRefs = refs ?? Array.Empty<string>(),
            };

        static FlowEdge Edge(string source, string target, bool added = false, string label = "", bool inferred = false) =>
            new FlowEdge
            {
                Source = source, Target = target, Type = FlowEdgeType.Next, Added = added, Label = label,
                Status = inferred ? FlowStatus.Inferred : FlowStatus.Explicit,
            };

        sealed class Sample
        {
            public string Id;
            public string Section;
            public string[] Objectives;
            public int Difficulty;
            public string Stage;
            public string Beat;
            public string Icon;
            public string Text;
            public QuestionPart[] Briefing;
            public QuestionPart Ask;
            public CastMember[] Cast;
            public AnswerTool[] Tools;
            public DecisionFlow Flow = DecisionFlow.Empty;
            public int Correct;
            public string[] Feedbacks;
            public string Explanation;
            public string[] Requirements = Array.Empty<string>();
            public AnswerOutcome[] Outcomes = Array.Empty<AnswerOutcome>();
            public DecisionFlow ResolutionFlow = DecisionFlow.Empty;
            public string Rule = string.Empty;
            public string Win = "The sample service moves on.";
            public string Fail = "The sample service stumbles, and the team digs in again.";
        }

        static KeyedQuestion Make(Sample sample) => new KeyedQuestion
        {
            Question = new Question
            {
                Id = sample.Id,
                Section = sample.Section,
                Source = "mock",
                Text = sample.Text,
                Tools = sample.Tools,
                Objectives = sample.Objectives,
                Requirements = sample.Requirements ?? Array.Empty<string>(),
                Difficulty = sample.Difficulty,
                Scenario = SampleScenario,
                Stage = sample.Stage,
                Beat = sample.Beat,
                Icon = sample.Icon,
                Briefing = sample.Briefing ?? Array.Empty<QuestionPart>(),
                Ask = sample.Ask,
                Cast = sample.Cast ?? Array.Empty<CastMember>(),
                Flow = sample.Flow,
            },
            Review = new AnswerReview
            {
                CorrectIndex = sample.Correct,
                Review = new PromptReview { Explanation = sample.Explanation, Feedbacks = sample.Feedbacks },
                Resolution = new Resolution
                {
                    AnswerRequirements = sample.Requirements,
                    Options = sample.Outcomes,
                    Flow = sample.ResolutionFlow,
                    Rule = sample.Rule,
                },
            },
            Win = sample.Win,
            Fail = sample.Fail,
        };

        // ---------- questions ----------

        /// <summary>Every question search can find: the round's, and some that only search reaches.</summary>
        static IReadOnlyList<KeyedQuestion> BuildCatalog() => BuildQuestions().Concat(BuildExtraQuestions()).ToList();

        const string Q1Failure = "Reports an authentication failure";
        const string Q1LogIn = "Tells the client to log in again";
        const string Q1Resource = "Keeps the resource's existence accurate";

        static IReadOnlyList<KeyedQuestion> BuildQuestions()
        {
            return new[]
            {
                Make(new Sample
                {
                    Id = "q1",
                    Section = "Sample: Web Fundamentals",
                    Objectives = new[] { "sample-http-status" },
                    Difficulty = 1,
                    Stage = "build",
                    Beat = "The first endpoint of the sample service answers, and nobody agrees what it should say.",
                    Icon = "api",
                    Text = "SCENARIO: A public website sends users to a login page whenever their session has expired. QUESTION: Which HTTP status code should the API return for a request that carries an expired session token?",
                    Briefing = new[]
                    {
                        Part(PartRole.Context, "A public website sends users to a login page whenever their session has expired.", "e1", "e2"),
                        Part(PartRole.Problem, "A request arrives carrying a session token that has already expired.", "e3", "e4"),
                        Part(PartRole.Goal, "The client should know that logging in again will fix it.", "e1"),
                    },
                    Ask = Part(PartRole.Ask, "Which HTTP status code should the API return for a request that carries an expired session token?", "e2", "e3"),
                    Cast = new[]
                    {
                        Member("e1", "User", CastType.Actor, "user"),
                        Member("e2", "API", CastType.System, "api"),
                        Member("e3", "Session Token", CastType.Artifact, "session"),
                        Member("e4", "Request", CastType.Object, "send"),
                        Member("e5", "Log In", CastType.Concept, "permission", scene: false),
                        Member("e6", "Permission", CastType.Concept, "permission", scene: false),
                    },
                    Tools = new[]
                    {
                        Tool(0, "400 Bad Request", "Blame the request's format", "send", "e4"),
                        Tool(1, "401 Unauthorized", "Ask for valid credentials", "permission", "e5", "e3"),
                        Tool(2, "403 Forbidden", "Refuse the known identity", "policy", "e6"),
                        Tool(3, "404 Not Found", "Say the resource is missing", "web", "e2"),
                    },
                    Flow = new DecisionFlow
                    {
                        Primary = "decision_flow",
                        Trigger = "A request with an expired token",
                        Nodes = new[]
                        {
                            Node("n1", FlowNodeType.Start, "Request arrives", refs: new[] { "e4" }),
                            Node("n2", FlowNodeType.Process, "Check the session token", refs: new[] { "e2", "e3" }),
                            Node("n3", FlowNodeType.Input, "The user expects the login page when the session is over", refs: new[] { "e1" }),
                            Node("n4", FlowNodeType.Output, "The token has expired", refs: new[] { "e3" }),
                            Node("n5", FlowNodeType.Decision, "Which status code?", isQuestion: true, refs: new[] { "e2", "e3" }),
                        },
                        Edges = new[]
                        {
                            Edge("n1", "n2"), Edge("n1", "n3", label: "a person is waiting", inferred: true), Edge("n2", "n4"),
                            Edge("n4", "n5"), Edge("n3", "n5", label: "what the client must do"),
                        },
                    },
                    Correct = 1,
                    Feedbacks = new[]
                    {
                        "400 means the request itself is malformed, which is not the problem here.",
                        "401 means valid authentication is required, so the client knows to log in again.",
                        "403 means the identity is known but not allowed, so logging in again would not help.",
                        "404 means the resource does not exist, which would mislead the client.",
                    },
                    Explanation = "HTTP separates authentication from authorization. An expired token is an authentication failure, which is what 401 reports.",
                    Requirements = new[] { Q1Failure, Q1LogIn, Q1Resource },
                    Outcomes = new[]
                    {
                        Outcome(0, false, AnswerImpact.Failure, "The client retries the same request and fails again.",
                            "400 blames the request's format, but the request is well formed.",
                            "A failed login is a problem with the request's format.",
                            new[] { Q1Resource }, new[] { "Doesn't report an authentication failure" }),
                        Outcome(1, true, AnswerImpact.Success, "The client sends the user to the login page, and the session starts again.",
                            "401 says valid credentials are needed, which is exactly what logging in again provides.",
                            meets: new[] { Q1Failure, Q1LogIn, Q1Resource }),
                        Outcome(2, false, AnswerImpact.Partial, "The user is told they aren't allowed, though logging in again would have worked.",
                            "403 says the identity is known and refused, so logging in again would not help.",
                            "An expired session means the user isn't allowed.",
                            new[] { Q1Failure, Q1Resource }, new[] { "Doesn't tell the client to log in again" }),
                        Outcome(3, false, AnswerImpact.Failure, "The client decides the page is gone and stops asking for it.",
                            "404 says the resource is missing, which sends the client looking in the wrong place.",
                            "Hiding the resource is a safe answer to any failed request.",
                            gaps: new[] { "Doesn't report an authentication failure", "Misleads the client about the resource" }),
                    },
                    Rule = "Missing or expired credentials are 401; known but not allowed is 403.",
                    // The backend reuses the decision's id for the step that replaces it, as here (n5).
                    ResolutionFlow = new DecisionFlow
                    {
                        Primary = "process_flow",
                        Trigger = "A request with an expired token",
                        Nodes = new[]
                        {
                            Node("n1", FlowNodeType.Start, "Request arrives", refs: new[] { "e4" }),
                            Node("n2", FlowNodeType.Process, "Check the session token", refs: new[] { "e2", "e3" }),
                            Node("n4", FlowNodeType.Output, "The token has expired", refs: new[] { "e3" }),
                            Node("n5", FlowNodeType.Process, "Answer 401 Unauthorized", added: true, refs: new[] { "e2", "e5" }),
                            Node("n6", FlowNodeType.End, "The client sends the user to log in again", added: true, refs: new[] { "e1", "e5" }),
                        },
                        Edges = new[]
                        {
                            Edge("n1", "n2"), Edge("n2", "n4"), Edge("n4", "n5", added: true, label: "authentication failed"),
                            Edge("n5", "n6", added: true),
                        },
                    },
                }),
                Make(new Sample
                {
                    Id = "q2",
                    Section = "Sample: Databases",
                    Objectives = new[] { "sample-query-performance" },
                    Difficulty = 1,
                    Stage = "scale",
                    Beat = "The sample service slows down as its tables grow.",
                    Icon = "database",
                    Text = "SCENARIO: A reporting query filters a table of ten million orders by customer, and it has become slow as the table has grown. QUESTION: Which change most directly speeds up this query?",
                    Briefing = new[]
                    {
                        Part(PartRole.Context, "A reporting query filters a table of ten million orders by customer.", "e1", "e2"),
                        Part(PartRole.Problem, "The query has become slow as the table has grown.", "e1"),
                    },
                    Ask = Part(PartRole.Ask, "Which change most directly speeds up this query?", "e1"),
                    Cast = new[]
                    {
                        Member("e1", "Query", CastType.Object, "search"),
                        Member("e2", "Order", CastType.Object, "order"),
                        Member("e3", "Index", CastType.Concept, "steps", scene: false),
                        Member("e4", "Connection Pool", CastType.System, "database", scene: false),
                    },
                    Tools = new[]
                    {
                        Tool(0, "Increase the connection pool size.", "Allow more connections", "database", "e4"),
                        Tool(1, "Add more columns to the query's result.", "Return more data", "steps", "e1"),
                        Tool(2, "Add an index on the customer column.", "Index the filtered column", "steps", "e3"),
                        Tool(3, "Run the query twice and keep the second result.", "Repeat the query", "search", "e1"),
                    },
                    Correct = 2,
                    Feedbacks = new[]
                    {
                        "More connections allow more concurrent queries but don't make this one faster.",
                        "Returning more data makes the query slower, not faster.",
                        "An index lets the database find matching rows without scanning the whole table.",
                        "Repeating the query only adds work.",
                    },
                    Explanation = "Without an index the database scans every row. An index on the filtered column turns that scan into a fast lookup.",
                }),
                Make(new Sample
                {
                    Id = "q3",
                    Section = "Sample: Version Control",
                    Objectives = new[] { "sample-git-history" },
                    Difficulty = 2,
                    Stage = "team",
                    Beat = "Two teammates changed the same history and want it sorted out.",
                    Icon = "commit",
                    Text = "SCENARIO: A developer has pushed a commit to a shared branch and later finds that it introduced a bug. QUESTION: What is the safest way to undo that commit?",
                    Briefing = new[]
                    {
                        Part(PartRole.Context, "A developer works on a branch the whole team pushes to.", "e1", "e2"),
                        Part(PartRole.CurrentState, "A commit that is already pushed introduced a bug.", "e3"),
                        Part(PartRole.Constraint, "Teammates may already have pulled the commit.", "e3"),
                    },
                    Ask = Part(PartRole.Ask, "What is the safest way to undo that commit?", "e3"),
                    Cast = new[]
                    {
                        Member("e1", "Developer", CastType.Actor, "user"),
                        Member("e2", "Shared Branch", CastType.Artifact, "branch"),
                        Member("e3", "Commit", CastType.Artifact, "commit"),
                        Member("e4", "git revert", CastType.Concept, "undo", scene: false),
                        Member("e5", "git reset --hard", CastType.Concept, "reset", scene: false),
                    },
                    Tools = new[]
                    {
                        Tool(0, "Run git revert on the commit and push the result.", "Undo with a new commit", "undo", "e4", "e3"),
                        Tool(1, "Run git reset --hard and force-push the branch.", "Rewrite the branch's history", "reset", "e5", "e2"),
                        Tool(2, "Delete the branch and recreate it from a teammate's copy.", "Recreate the branch", "branch", "e2"),
                        Tool(3, "Edit the files back by hand without committing.", "Fix the files locally", "file", "e1"),
                    },
                    Correct = 0,
                    Feedbacks = new[]
                    {
                        "A revert adds a new commit that undoes the change, so shared history stays intact.",
                        "A force-push rewrites shared history and can discard teammates' work.",
                        "This is disruptive and can lose any work that isn't in that copy.",
                        "Uncommitted edits never reach the shared branch, so the bug stays.",
                    },
                    Explanation = "On shared branches, prefer adding a commit over rewriting history. git revert does that.",
                    Outcomes = new[]
                    {
                        Outcome(0, true, AnswerImpact.Success, "The fix lands as a new commit and nobody's checkout breaks.",
                            "A revert adds a commit that undoes the change, so nobody's history is rewritten."),
                        Outcome(1, false, AnswerImpact.Failure, "Teammates' next pull fails, and work they pushed is gone.",
                            "A hard reset and force-push rewrite history that teammates already have.",
                            "Rewriting a pushed commit is harmless if you force-push.", gaps: new[] { "Keeps shared history intact" }),
                        Outcome(2, false, AnswerImpact.Partial, "The bug is gone, along with a morning of someone's work.",
                            "Recreating the branch is disruptive and loses work that isn't in that copy.",
                            "Any copy of a branch is as good as the shared one.", gaps: new[] { "Keeps everyone's work" }),
                        Outcome(3, false, AnswerImpact.Failure, "The bug stays on the branch for everyone else.",
                            "Edits that are never committed never reach the branch.",
                            "Local edits change the shared branch.", gaps: new[] { "Actually changes the shared branch" }),
                    },
                    Rule = "On a shared branch, undo with a new commit instead of rewriting history.",
                }),
            };
        }

        static IEnumerable<KeyedQuestion> BuildExtraQuestions()
        {
            yield return Make(new Sample
            {
                Id = "q4",
                Section = "Sample: Web Fundamentals",
                Objectives = new[] { "sample-browser-security", "sample-http-status" },
                Difficulty = 2,
                Stage = "incident",
                Beat = "The browser refuses a request that works everywhere else.",
                Icon = "web",
                Text = "SCENARIO: A browser page loads data from an API on another domain, and the request fails before the API sees it. QUESTION: Which mechanism decides whether the browser may make that request?",
                Briefing = new[]
                {
                    Part(PartRole.Context, "A browser page loads data from an API on another domain.", "e1", "e2"),
                    Part(PartRole.Problem, "The request fails before the API sees it.", "e2"),
                },
                Ask = Part(PartRole.Ask, "Which mechanism decides whether the browser may make that request?", "e1"),
                Cast = new[]
                {
                    Member("e1", "Browser", CastType.System, "web"),
                    Member("e2", "API", CastType.ExternalService, "api"),
                    Member("e3", "CORS", CastType.Concept, "policy", scene: false),
                },
                Tools = new[]
                {
                    Tool(0, "DNS resolution", "Look up the address", "server"),
                    Tool(1, "TLS certificate pinning", "Restrict trusted certificates", "security"),
                    Tool(2, "HTTP keep-alive", "Reuse connections", "link"),
                    Tool(3, "CORS (Cross-Origin Resource Sharing)", "Check the server's origin rules", "policy", "e3"),
                },
                Correct = 3,
                Feedbacks = new[]
                {
                    "DNS only finds the server's address; it says nothing about which pages may call it.",
                    "Pinning restricts which certificates a client trusts, not which origins may call the API.",
                    "Keep-alive reuses connections; it doesn't allow or block requests.",
                    "The browser checks the API's CORS headers before letting a page on another origin read the response.",
                },
                Explanation = "Browsers block cross-origin requests unless the server allows them with CORS headers, often after a preflight request.",
            });
            yield return Make(new Sample
            {
                Id = "q5",
                Section = "Sample: Databases",
                Objectives = new[] { "sample-transactions" },
                Difficulty = 3,
                Stage = "incident",
                Beat = "A transfer left money in neither account.",
                Icon = "transfer",
                Text = "SCENARIO: Two transfers run at the same time against the same bank account, and the final balance is wrong. QUESTION: Which database feature prevents this kind of lost update?",
                Briefing = new[]
                {
                    Part(PartRole.CurrentState, "Two transfers run at the same time against the same bank account.", "e1", "e2"),
                    Part(PartRole.Problem, "The final balance is wrong.", "e2"),
                },
                Ask = Part(PartRole.Ask, "Which database feature prevents this kind of lost update?"),
                Cast = new[]
                {
                    Member("e1", "Transfer", CastType.Object, "transfer"),
                    Member("e2", "Account", CastType.Object, "calculation"),
                },
                Tools = new[]
                {
                    Tool(0, "Transactions with proper isolation", "Isolate concurrent writes", "transfer"),
                    Tool(1, "A larger buffer pool", "Add memory", "memory"),
                    Tool(2, "Read replicas", "Spread the reads", "copy"),
                    Tool(3, "Column compression", "Compress the data", "compress"),
                },
                Correct = 0,
                Feedbacks = new[]
                {
                    "Isolation makes concurrent transactions behave as if they ran one after another, so no update is lost.",
                    "More memory speeds queries up but doesn't coordinate concurrent writes.",
                    "Replicas spread reads; the conflicting writes still hit the primary.",
                    "Compression saves space and has nothing to do with concurrency.",
                },
                Explanation = "A lost update happens when two writes interleave. Transactions with a suitable isolation level, or row locks, serialize them.",
            });
        }
    }
}
