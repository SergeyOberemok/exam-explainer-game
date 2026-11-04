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
                // Like the backend's search results: no simplified text until the question is added.
                .Select(q => new Question { Id = q.Id, Section = q.Section, Source = q.Source, Original = q.Original })
                .ToList();
        }

        sealed class Version
        {
            public string Question;
            public string[] Answers;
            public string[] Feedbacks;
            public string Explanation;
        }

        static KeyedQuestion Make(string id, string section, int correctIndex, Version original, Version simplified) =>
            new KeyedQuestion
            {
                Question = new Question
                {
                    Id = id,
                    Section = section,
                    Source = "mock",
                    Original = new QuestionPrompt { Question = original.Question, Answers = original.Answers },
                    Simplified = new QuestionPrompt { Question = simplified.Question, Answers = simplified.Answers },
                },
                Review = new AnswerReview
                {
                    CorrectIndex = correctIndex,
                    Original = new PromptReview { Explanation = original.Explanation, Feedbacks = original.Feedbacks },
                    Simplified = new PromptReview { Explanation = simplified.Explanation, Feedbacks = simplified.Feedbacks },
                },
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
                    Feedbacks = new[]
                    {
                        "Wrong. DNS only finds the server.",
                        "Wrong. That's about trusting certificates.",
                        "Wrong. That's about reusing connections.",
                        "Right. The server's CORS headers tell the browser what's allowed.",
                    },
                    Explanation = "CORS headers from the server tell the browser which other sites may call it.",
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
                    Feedbacks = new[]
                    {
                        "Right. They keep concurrent writes from overwriting each other.",
                        "Wrong. Memory doesn't coordinate writes.",
                        "Wrong. Writes still go to one place.",
                        "Wrong. That only saves space.",
                    },
                    Explanation = "Transactions make concurrent changes safe.",
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
                        Feedbacks = new[]
                        {
                            "Wrong. The request is well formed.",
                            "Right. 401 tells the client to log in again.",
                            "Wrong. Logging in again wouldn't fix a 403.",
                            "Wrong. The resource still exists.",
                        },
                        Explanation = "An expired login is an authentication failure, and 401 is the code for that.",
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
                        Feedbacks = new[]
                        {
                            "Wrong. This doesn't make a single query faster.",
                            "Wrong. This makes the query slower.",
                            "Right. The database can jump straight to the matching rows.",
                            "Wrong. This only adds work.",
                        },
                        Explanation = "An index lets the database skip scanning the whole table.",
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
                        Feedbacks = new[]
                        {
                            "Right. It undoes the change and keeps history.",
                            "Wrong. It can erase teammates' work.",
                            "Wrong. It's disruptive and can lose work.",
                            "Wrong. The fix never reaches the branch.",
                        },
                        Explanation = "git revert undoes a commit by adding a new one, so nobody's history breaks.",
                    }),
            };
        }
    }
}
