using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// Stand-in for live data, used by tests and player builds:
    /// returns a fixed set of questions after a short fake latency.
    /// </summary>
    public sealed class MockQuestionService : IQuestionService
    {
        readonly int _latencyMs;

        public MockQuestionService(int latencyMs = 300)
        {
            _latencyMs = latencyMs;
        }

        public async Task<IReadOnlyList<Question>> GetQuestionsAsync(CancellationToken cancellationToken = default)
        {
            if (_latencyMs > 0)
                await Task.Delay(_latencyMs, cancellationToken);
            return BuildQuestions();
        }

        static IReadOnlyList<Question> BuildQuestions()
        {
            return new[]
            {
                new Question
                {
                    Id = "q1",
                    Section = "Sample: Web Fundamentals",
                    Source = "mock",
                    CorrectIndex = 1,
                    Original = new QuestionPrompt
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
                    Simplified = new QuestionPrompt
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
                    },
                },
                new Question
                {
                    Id = "q2",
                    Section = "Sample: Databases",
                    Source = "mock",
                    CorrectIndex = 2,
                    Original = new QuestionPrompt
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
                    Simplified = new QuestionPrompt
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
                    },
                },
                new Question
                {
                    Id = "q3",
                    Section = "Sample: Version Control",
                    Source = "mock",
                    CorrectIndex = 0,
                    Original = new QuestionPrompt
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
                    Simplified = new QuestionPrompt
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
                    },
                },
            };
        }
    }
}
