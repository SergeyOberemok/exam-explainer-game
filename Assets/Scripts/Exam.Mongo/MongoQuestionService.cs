using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UnityEditor;
using UnityEngine;

namespace ExamCards.Exam.Mongo
{
    /// <summary>
    /// Development-only question source that reads MongoDB directly, using DATABASE_URL from .env.
    /// This assembly and the driver are Editor-only, so neither the driver nor the credentials reach a player build.
    /// </summary>
    public sealed class MongoQuestionService : IQuestionService
    {
        const string DatabaseName = "exam-explainer";
        const string OriginalsCollection = "examination";
        const string SimplifiedCollection = "examination_simplified";
        const string UrlKey = "DATABASE_URL";

        [InitializeOnLoadMethod]
        static void Register() => QuestionServices.Factory = () => new MongoQuestionService();

        public async Task<IReadOnlyList<Question>> GetQuestionsAsync(CancellationToken cancellationToken = default)
        {
            // Read on the calling thread: Application.dataPath is main-thread only.
            var url = ReadDatabaseUrl();

            // The driver resolves the mongodb+srv address synchronously, so keep it off the main thread.
            return await Task.Run(() => LoadAsync(url, cancellationToken), cancellationToken);
        }

        static async Task<IReadOnlyList<Question>> LoadAsync(string url, CancellationToken cancellationToken)
        {
            var settings = MongoClientSettings.FromConnectionString(url);

            // Atlas asks for an optional client certificate. Under TLS 1.3 that request arrives before the
            // server's certificate, and UnityTLS fails the handshake, so stay on TLS 1.2.
            settings.SslSettings = new SslSettings { EnabledSslProtocols = SslProtocols.Tls12 };

            using var client = new MongoClient(settings);
            var database = client.GetDatabase(DatabaseName);
            var originals = await ReadItemsAsync(database, OriginalsCollection, cancellationToken);
            var simplifieds = await ReadItemsAsync(database, SimplifiedCollection, cancellationToken);
            return QuestionAssembler.Build(originals, simplifieds);
        }

        static async Task<List<ExamItem>> ReadItemsAsync(IMongoDatabase database, string collection, CancellationToken cancellationToken)
        {
            var documents = await database.GetCollection<BsonDocument>(collection)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                .ToListAsync(cancellationToken);
            return documents.Select(ToExamItem).ToList();
        }

        static ExamItem ToExamItem(BsonDocument document)
        {
            var prompt = document["prompt"].AsBsonDocument;
            return new ExamItem
            {
                Id = document["_id"].ToString(),
                QuestionId = document.GetValue("question_id", BsonNull.Value).IsString ? document["question_id"].AsString : null,
                Section = document["section"].AsString,
                Source = document["source"].AsString,
                CorrectResponse = ToStrings(document["correct_response"]),
                Prompt = new QuestionPrompt
                {
                    Question = prompt["question"].AsString,
                    Answers = ToStrings(prompt["answers"]),
                    Feedbacks = ToStrings(prompt["feedbacks"]),
                    Explanation = prompt["explanation"].AsString,
                },
            };
        }

        static string[] ToStrings(BsonValue array) => array.AsBsonArray.Select(value => value.AsString).ToArray();

        static string ReadDatabaseUrl()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var path = Path.Combine(projectRoot, ".env");
            if (!File.Exists(path))
                throw new FileNotFoundException($"No .env file in the project root; it must define {UrlKey}.", path);

            var prefix = UrlKey + "=";
            var line = File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
            var url = line?.Substring(prefix.Length).Trim().Trim('"', '\'');
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException($".env does not define {UrlKey}.");
            return url;
        }
    }
}
