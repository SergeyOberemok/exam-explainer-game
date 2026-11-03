using System;
using UnityEngine;

namespace ExamCards.Exam.Api
{
    /// <summary>
    /// Where the backend lives and how big a round is. Loaded from <c>Resources/ExamApiSettings</c>;
    /// the defaults below apply when that asset is missing.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "ExamCards/Exam API Settings")]
    public sealed class ExamApiSettings : ScriptableObject
    {
        public const string ResourceName = "ExamApiSettings";
        public const int MaxQuestionCount = 50;

        [Tooltip("Base URL of the backend, without a trailing slash. Plain http:// works only in the Editor and development builds.")]
        [SerializeField] string _baseUrl = "http://127.0.0.1:8000";

        [Tooltip("Questions requested for one round.")]
        [Range(1, MaxQuestionCount)]
        [SerializeField] int _questionCount = 10;

        [Tooltip("Seconds before a single request gives up.")]
        [Min(1)]
        [SerializeField] int _timeoutSeconds = 15;

        public string BaseUrl => _baseUrl;
        public int QuestionCount => _questionCount;
        public int TimeoutSeconds => _timeoutSeconds;

        public static ExamApiSettings Load()
        {
            var settings = Resources.Load<ExamApiSettings>(ResourceName);
            return settings != null ? settings : CreateInstance<ExamApiSettings>();
        }

        /// <summary>True for an absolute http:// or https:// URL without a query or fragment.</summary>
        public static bool TryValidateBaseUrl(string baseUrl, out string error)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "The API base URL is empty.";
                return false;
            }
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = $"The API base URL '{baseUrl}' is not an absolute http:// or https:// URL.";
                return false;
            }
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                error = $"The API base URL '{baseUrl}' must not have a query or fragment.";
                return false;
            }
            error = null;
            return true;
        }

        void OnValidate()
        {
            if (!TryValidateBaseUrl(_baseUrl, out var error))
                Debug.LogWarning(error, this);
        }
    }
}
