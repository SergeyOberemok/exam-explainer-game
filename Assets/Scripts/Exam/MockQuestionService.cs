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
    /// </summary>
    public sealed class MockQuestionService : IQuestionService, IQuestionSearch
    {
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
            return BuildCatalog()
                .Select(keyed => keyed.Question)
                .Where(q => Matches(q.Original.Question) || Matches(q.Section) || q.Original.Answers.Any(Matches))
                // Like the backend's search results: no simplified text, entities or parts until the question is added.
                .Select(q => new Question
                {
                    Id = q.Id,
                    Section = q.Section,
                    Source = q.Source,
                    Original = new QuestionPrompt { Question = q.Original.Question, Answers = q.Original.Answers },
                })
                .ToList();
        }

        sealed class Version
        {
            public string Question;
            public string[] Answers;
            public string[] Feedbacks;
            public string Explanation;
            public PromptEntities Entities;
        }

        /// <summary>Icon keys for the sample entities, as the backend would pick them; the rest get their kind's icon.</summary>
        static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            ["Customer"] = "user", ["User"] = "user", ["Account"] = "person",
            ["API"] = "api", ["Call API"] = "send", ["Request"] = "send", ["Return Status Code"] = "result",
            ["Log In"] = "permission", ["Permission"] = "permission", ["Session"] = "session",
            ["Browser"] = "web", ["Web Page"] = "web", ["Page"] = "web", ["DNS"] = "server",
            ["CORS"] = "policy", ["Certificate"] = "security", ["Connection"] = "link",
            ["Database Connection"] = "database", ["Query"] = "search", ["Run Query"] = "search", ["Index"] = "steps",
            ["Order"] = "order", ["Balance"] = "calculation", ["Transfer"] = "transfer", ["Transaction"] = "transfer",
            ["Memory"] = "memory", ["Read Replica"] = "copy", ["Compression"] = "compress",
            ["File"] = "file", ["Branch"] = "branch", ["Commit"] = "commit", ["Push"] = "upload", ["Force-Push"] = "upload",
            ["Undo Commit"] = "undo", ["git revert"] = "undo", ["git reset --hard"] = "reset",
        };

        static VisualEntity Entity(string name, EntityKind kind, string[] aliases) =>
            new VisualEntity { Name = name, Kind = kind, Aliases = aliases, Icon = Icons.TryGetValue(name, out var icon) ? icon : null };

        static VisualEntity Actor(string name, params string[] aliases) => Entity(name, EntityKind.Actor, aliases);
        static VisualEntity Thing(string name, params string[] aliases) => Entity(name, EntityKind.Object, aliases);
        static VisualEntity Step(string name, params string[] aliases) => Entity(name, EntityKind.Action, aliases);

        /// <summary>The question takes the icon of its first entity and each answer that of its own first one.</summary>
        static PromptEntities Entities(VisualEntity[] question, params VisualEntity[][] answers) =>
            new PromptEntities
            {
                QuestionIcon = EntityIconResolver.Resolve(question[0]),
                Question = question,
                Answers = answers,
                AnswerIcons = answers.Select(answer => EntityIconResolver.Resolve(answer[0])).ToArray(),
            };

        /// <summary>Objective keys (main one first) and difficulty of the sample questions, like the backend's tags.</summary>
        static readonly Dictionary<string, (string[] Objectives, int Difficulty)> Tags =
            new Dictionary<string, (string[] Objectives, int Difficulty)>
            {
                ["q1"] = (new[] { "sample-http-status" }, 1),
                ["q2"] = (new[] { "sample-query-performance" }, 1),
                ["q3"] = (new[] { "sample-git-history" }, 2),
                ["q4"] = (new[] { "sample-browser-security", "sample-http-status" }, 2),
                ["q5"] = (new[] { "sample-transactions" }, 3),
            };

        /// <summary>Chapter, stage and story line of the sample questions, like the backend's story records.</summary>
        static readonly Dictionary<string, (string Stage, string Beat)> Story =
            new Dictionary<string, (string Stage, string Beat)>
            {
                ["q1"] = ("build", "The first endpoint of the sample service answers, and nobody agrees what it should say."),
                ["q2"] = ("scale", "The sample service slows down as its tables grow."),
                ["q3"] = ("team", "Two teammates changed the same history and want it sorted out."),
                ["q4"] = ("incident", "The browser refuses a request that works everywhere else."),
                ["q5"] = ("incident", "A transfer left money in neither account."),
            };

        /// <summary>The chapter every sample question of the round belongs to.</summary>
        public const string SampleScenario = "sample-service";

        /// <summary>
        /// The original's feedback and explanation are the only review, as on the backend; the simplified version
        /// has no review of its own. Both texts get the same entities.
        /// </summary>
        static KeyedQuestion Make(string id, string section, int correctIndex, Version original, Version simplified)
        {
            var entities = simplified.Entities ?? PromptEntities.Empty;
            Decompositions.TryGetValue(id, out var decomposition);
            return new KeyedQuestion
            {
                Question = new Question
                {
                    Id = id,
                    Section = section,
                    Source = "mock",
                    Objectives = Tags[id].Objectives,
                    Difficulty = Tags[id].Difficulty,
                    Scenario = SampleScenario,
                    Stage = Story[id].Stage,
                    Beat = Story[id].Beat,
                    Original = new QuestionPrompt { Question = original.Question, Answers = original.Answers, Entities = entities },
                    Simplified = new QuestionPrompt { Question = simplified.Question, Answers = simplified.Answers, Entities = entities },
                    Parts = decomposition.Parts ?? Array.Empty<QuestionPart>(),
                    Flow = decomposition.Flow ?? DecisionFlow.Empty,
                },
                Review = new AnswerReview
                {
                    CorrectIndex = correctIndex,
                    Outcome = "The sample service moves on.",
                    Original = new PromptReview { Explanation = original.Explanation, Feedbacks = original.Feedbacks },
                    Resolution = decomposition.Resolution ?? Resolution.Empty,
                },
            };
        }

        static QuestionPart Part(PartRole role, string text) => new QuestionPart { Role = role, Text = text };

        static FlowNode Node(string id, FlowNodeType type, string label, bool isQuestion = false, bool added = false) =>
            new FlowNode { Id = id, Type = type, Label = label, Status = FlowStatus.Explicit, IsQuestion = isQuestion, Added = added };

        static FlowEdge Edge(string source, string target, bool added = false) =>
            new FlowEdge { Source = source, Target = target, Type = FlowEdgeType.Next, Status = FlowStatus.Explicit, Added = added };

        static OptionEvaluation Option(int index, bool correct, string reason, params string[] gaps) =>
            new OptionEvaluation { Index = index, IsCorrect = correct, Reason = reason, Gaps = gaps };

        static OptionEvaluation Meets(OptionEvaluation option, params string[] requirements)
        {
            option.SupportedRequirements = requirements;
            return option;
        }

        const string Q1Failure = "Reports an authentication failure";
        const string Q1LogIn = "Tells the client to log in again";
        const string Q1Resource = "Keeps the resource's existence accurate";

        /// <summary>What the backend's decomposition and resolution would add, for two of the sample questions.</summary>
        static readonly Dictionary<string, (QuestionPart[] Parts, DecisionFlow Flow, Resolution Resolution)> Decompositions =
            new Dictionary<string, (QuestionPart[] Parts, DecisionFlow Flow, Resolution Resolution)>
            {
                ["q1"] = (
                    new[]
                    {
                        Part(PartRole.Context, "A public website sends users to a login page whenever their session has expired."),
                        Part(PartRole.Problem, "A request arrives carrying a session token that has already expired."),
                        Part(PartRole.Goal, "The client should know that logging in again will fix it."),
                        Part(PartRole.Ask, "Which HTTP status code should the API return for a request that carries an expired session token?"),
                    },
                    new DecisionFlow
                    {
                        Primary = "decision_flow",
                        Trigger = "A request with an expired token",
                        Nodes = new[]
                        {
                            Node("n1", FlowNodeType.Start, "Request arrives"),
                            Node("n2", FlowNodeType.Process, "Check the session token"),
                            Node("n3", FlowNodeType.Decision, "Which status code?", isQuestion: true),
                        },
                        Edges = new[] { Edge("n1", "n2"), Edge("n2", "n3") },
                    },
                    new Resolution
                    {
                        AnswerRequirements = new[] { Q1Failure, Q1LogIn, Q1Resource },
                        Options = new[]
                        {
                            Meets(Option(0, false, "400 blames the request's format, but the request is well formed.", "Doesn't report an authentication failure"), Q1Resource),
                            Meets(Option(1, true, "401 says valid credentials are needed, which is exactly what logging in again provides."), Q1Failure, Q1LogIn, Q1Resource),
                            Meets(Option(2, false, "403 says the identity is known and refused, so logging in again would not help.", "Doesn't tell the client to log in again"), Q1Failure, Q1Resource),
                            Option(3, false, "404 says the resource is missing, which sends the client looking in the wrong place.", "Doesn't report an authentication failure", "Misleads the client about the resource"),
                        },
                        Rule = "Missing or expired credentials are 401; known but not allowed is 403.",
                    }),
                ["q3"] = (
                    new[]
                    {
                        Part(PartRole.Context, "A developer works on a branch the whole team pushes to."),
                        Part(PartRole.CurrentState, "A commit that is already pushed introduced a bug."),
                        Part(PartRole.Constraint, "Teammates may already have pulled the commit."),
                        Part(PartRole.Ask, "What is the safest way to undo that commit?"),
                    },
                    DecisionFlow.Empty,
                    new Resolution
                    {
                        Options = new[]
                        {
                            Option(0, true, "A revert adds a commit that undoes the change, so nobody's history is rewritten."),
                            Option(1, false, "A hard reset and force-push rewrite history that teammates already have.", "Keeps shared history intact"),
                            Option(2, false, "Recreating the branch is disruptive and loses work that isn't in that copy.", "Keeps everyone's work"),
                            Option(3, false, "Edits that are never committed never reach the branch.", "Actually changes the shared branch"),
                        },
                        Rule = "On a shared branch, undo with a new commit instead of rewriting history.",
                    }),
            };

        /// <summary>Every question search can find: the round's, and some that only search reaches.</summary>
        static IReadOnlyList<KeyedQuestion> BuildCatalog() => BuildQuestions().Concat(BuildExtraQuestions()).ToList();

        static IEnumerable<KeyedQuestion> BuildExtraQuestions()
        {
            yield return Make("q4", "Sample: Web Fundamentals", 3,
                new Version
                {
                    Question = "SCENARIO: A browser page loads data from an API on another domain, and the request fails before the API sees it. QUESTION: Which mechanism decides whether the browser may make that request?",
                    Answers = new[] { "DNS resolution", "TLS certificate pinning", "HTTP keep-alive", "CORS (Cross-Origin Resource Sharing)" },
                    Feedbacks = new[]
                    {
                        "DNS only finds the server's address; it says nothing about which pages may call it.",
                        "Pinning restricts which certificates a client trusts, not which origins may call the API.",
                        "Keep-alive reuses connections; it doesn't allow or block requests.",
                        "The browser checks the API's CORS headers before letting a page on another origin read the response.",
                    },
                    Explanation = "Browsers block cross-origin requests unless the server allows them with CORS headers, often after a preflight request.",
                },
                new Version
                {
                    Question = "A web page can't call an API on another domain. What controls whether the browser allows it?",
                    Answers = new[] { "DNS", "Certificate pinning", "Keep-alive", "CORS" },
                    Entities = Entities(new[] { Thing("Web Page", "page"), Thing("Browser"), Thing("API"), Step("Call API") },
                        new[] { Thing("DNS") }, new[] { Thing("Certificate") }, new[] { Thing("Connection") }, new[] { Thing("CORS", "CORS headers") }),
                });
            yield return Make("q5", "Sample: Databases", 0,
                new Version
                {
                    Question = "SCENARIO: Two transfers run at the same time against the same bank account, and the final balance is wrong. QUESTION: Which database feature prevents this kind of lost update?",
                    Answers = new[] { "Transactions with proper isolation", "A larger buffer pool", "Read replicas", "Column compression" },
                    Feedbacks = new[]
                    {
                        "Isolation makes concurrent transactions behave as if they ran one after another, so no update is lost.",
                        "More memory speeds queries up but doesn't coordinate concurrent writes.",
                        "Replicas spread reads; the conflicting writes still hit the primary.",
                        "Compression saves space and has nothing to do with concurrency.",
                    },
                    Explanation = "A lost update happens when two writes interleave. Transactions with a suitable isolation level, or row locks, serialize them.",
                },
                new Version
                {
                    Question = "Two transfers to one account at the same time leave a wrong balance. What prevents this?",
                    Answers = new[] { "Transactions", "More memory", "Read replicas", "Compression" },
                    Entities = Entities(new[] { Step("Transfer", "transfers"), Thing("Account"), Thing("Balance") },
                        new[] { Thing("Transaction", "transactions") }, new[] { Thing("Memory") }, new[] { Thing("Read Replica", "read replicas") }, new[] { Thing("Compression") }),
                });
        }

        static IReadOnlyList<KeyedQuestion> BuildQuestions()
        {
            return new[]
            {
                Make("q1", "Sample: Web Fundamentals", 1,
                    new Version
                    {
                        Question = "SCENARIO: A public website sends users to a login page whenever their session has expired. QUESTION: Which HTTP status code should the API return for a request that carries an expired session token?",
                        Answers = new[] { "400 Bad Request", "401 Unauthorized", "403 Forbidden", "404 Not Found" },
                        Feedbacks = new[]
                        {
                            "400 means the request itself is malformed, which is not the problem here.",
                            "401 means valid authentication is required, so the client knows to log in again.",
                            "403 means the identity is known but not allowed, so logging in again would not help.",
                            "404 means the resource does not exist, which would mislead the client.",
                        },
                        Explanation = "HTTP separates authentication from authorization. An expired token is an authentication failure, which is what 401 reports.",
                    },
                    new Version
                    {
                        Question = "A user's login session has expired. Which status code should the API return?",
                        Answers = new[] { "400: the request is malformed", "401: the user must log in", "403: the user isn't allowed", "404: the page doesn't exist" },
                        Entities = Entities(new[] { Actor("User", "user's"), Thing("Session", "login session"), Thing("API"), Step("Return Status Code") },
                            new[] { Thing("Request") }, new[] { Step("Log In") }, new[] { Thing("Permission") }, new[] { Thing("Page") }),
                    }),
                Make("q2", "Sample: Databases", 2,
                    new Version
                    {
                        Question = "SCENARIO: A reporting query filters a table of ten million orders by customer, and it has become slow as the table has grown. QUESTION: Which change most directly speeds up this query?",
                        Answers = new[] { "Increase the connection pool size.", "Add more columns to the query's result.", "Add an index on the customer column.", "Run the query twice and keep the second result." },
                        Feedbacks = new[]
                        {
                            "More connections allow more concurrent queries but don't make this one faster.",
                            "Returning more data makes the query slower, not faster.",
                            "An index lets the database find matching rows without scanning the whole table.",
                            "Repeating the query only adds work.",
                        },
                        Explanation = "Without an index the database scans every row. An index on the filtered column turns that scan into a fast lookup.",
                    },
                    new Version
                    {
                        Question = "A query that finds orders by customer has become slow. What helps most?",
                        Answers = new[] { "More database connections", "Returning more columns", "An index on the customer column", "Running the query twice" },
                        Entities = Entities(new[] { Thing("Query"), Thing("Order", "orders"), Actor("Customer") },
                            new[] { Thing("Database Connection", "database connections") }, new[] { Thing("Column", "columns") }, new[] { Thing("Index") }, new[] { Step("Run Query") }),
                    }),
                Make("q3", "Sample: Version Control", 0,
                    new Version
                    {
                        Question = "SCENARIO: A developer has pushed a commit to a shared branch and later finds that it introduced a bug. QUESTION: What is the safest way to undo that commit?",
                        Answers = new[] { "Run git revert on the commit and push the result.", "Run git reset --hard and force-push the branch.", "Delete the branch and recreate it from a teammate's copy.", "Edit the files back by hand without committing." },
                        Feedbacks = new[]
                        {
                            "A revert adds a new commit that undoes the change, so shared history stays intact.",
                            "A force-push rewrites shared history and can discard teammates' work.",
                            "This is disruptive and can lose any work that isn't in that copy.",
                            "Uncommitted edits never reach the shared branch, so the bug stays.",
                        },
                        Explanation = "On shared branches, prefer adding a commit over rewriting history. git revert does that.",
                    },
                    new Version
                    {
                        Question = "You pushed a bad commit to a shared branch. What is the safest way to undo it?",
                        Answers = new[] { "git revert, then push", "git reset --hard, then force-push", "Delete and recreate the branch", "Fix the files but don't commit" },
                        Entities = Entities(new[] { Thing("Commit", "bad commit"), Thing("Branch", "shared branch"), Step("Undo Commit") },
                            new[] { Thing("git revert"), Step("Push") }, new[] { Thing("git reset --hard"), Step("Force-Push") }, new[] { Thing("Branch") }, new[] { Thing("File", "files") }),
                    }),
            };
        }
    }
}
