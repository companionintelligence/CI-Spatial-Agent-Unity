using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Llm
{
    /// <summary>
    /// The reply check's questions to IBM Granite Guardian on Ollama, and how its answers are read.
    /// <para><b>Model.</b> <c>granite3-guardian:2b</c> (<see cref="LlmConfig.DefaultSafetyModel"/>), the smallest Granite
    /// Guardian in the Ollama library, made to detect risks in prompts and in responses
    /// (https://ollama.com/library/granite3-guardian: tags 2b = latest, 2.7 GB, and 8b, 5.8 GB; licence Apache 2.0, as on
    /// IBM's model card https://huggingface.co/ibm-granite/granite-guardian-3.0-2b).</para>
    /// <para><b>Question.</b> One <c>/api/chat</c> request per risk. The system message is the risk name (the library page
    /// selects a risk by setting the system prompt); the user message is the user's question and the assistant message
    /// is the reply. The model's Ollama template judges the last message, the reply, with the message before it as
    /// context, against the risk's definition, and ends "Your answer must be either 'Yes' or 'No'." The model's Ollama
    /// parameters set temperature 0; the request does too.</para>
    /// <para><b>Answer.</b> A single token, Yes or No (library page); Yes means the risk is present (model card). Granite
    /// Guardian 3.3 and 4.1 answer in <c>&lt;score&gt; yes &lt;/score&gt;</c> tags
    /// (https://ollama.com/library/granite4.1-guardian), which are read too. Anything else, an empty answer, an error or
    /// a missing model is no answer, and the reply is withheld.</para>
    /// <para>The request carries the risk name, the question and the reply, nothing else: not the scene context, the
    /// persona or the safety prompt. It goes to the language model's own server (<see cref="LlmConfig.BaseUrl"/>), which
    /// already receives both.</para>
    /// </summary>
    public static class GraniteGuardian
    {
        /// <summary>
        /// The risks every reply is checked for, from the model card's harm categories ("jailbreak" judges prompts, and
        /// the RAG risks need retrieved context, so neither applies). "harm" is the umbrella category; the others
        /// cover what the store sheets name: sexual content (also with minors), violence, weapons and self-harm, hate and
        /// demeaning stereotypes, crime, fraud and deception, insults and harassment. This list is code, not configuration.
        /// </summary>
        public static readonly IReadOnlyList<string> Risks = new[]
        {
            "harm",
            "sexual_content",
            "violence",
            "social_bias",
            "unethical_behavior",
            "profanity",
        };

        /// <summary>The path of the request on the language model's server.</summary>
        public const string ChatPath = "/api/chat";

        [Serializable]
        private class Message
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class Options
        {
            public float temperature;
        }

        [Serializable]
        private class Request
        {
            public string model;
            public Message[] messages;
            public bool stream;
            public Options options;
        }

        [Serializable]
        private class Response
        {
            public Message message;
            public string error;
        }

        private static readonly Regex Score = new Regex(@"<score>\s*(yes|no)\s*</score>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The body of the request that asks <paramref name="model"/> whether <paramref name="reply"/> carries <paramref name="risk"/>.</summary>
        public static string BuildRequestJson(string model, string risk, string question, string reply)
        {
            return JsonUtility.ToJson(new Request
            {
                model = model ?? string.Empty,
                messages = new[]
                {
                    new Message { role = "system", content = risk ?? string.Empty },
                    new Message { role = "user", content = question ?? string.Empty },
                    new Message { role = "assistant", content = reply ?? string.Empty },
                },
                stream = false,
                options = new Options { temperature = 0f },
            });
        }

        /// <summary>
        /// Reads one answer. <paramref name="status"/> is the HTTP status (0 when the server was not reached),
        /// <paramref name="body"/> the response text and <paramref name="transportError"/> the request's error, null when
        /// it succeeded. Yes is <see cref="ReplyVerdict.Blocked"/>, No is <see cref="ReplyVerdict.Safe"/>, anything else
        /// is <see cref="ReplyVerdict.Unavailable"/> with a reason that names the model.
        /// </summary>
        public static ReplyCheckResult ReadAnswer(string model, string risk, long status, string body, string transportError)
        {
            // Ollama answers a request for a model it does not have with 404 and {"error":"model '...' not found"}.
            string serverError = ErrorOf(body);
            if (serverError != null && serverError.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
                return ReplyCheckResult.Unavailable(model + " is not installed on the LLM server. Install it there: ollama pull " + model);
            if (!string.IsNullOrEmpty(transportError) || status >= 400)
            {
                if (status == 0) return ReplyCheckResult.Unavailable("the LLM server could not be reached for the safety check.");
                return ReplyCheckResult.Unavailable("the LLM server refused the safety check with " + model + " (HTTP " + status + ").");
            }
            if (serverError != null)
                return ReplyCheckResult.Unavailable("the LLM server reported an error running " + model + ".");

            Response response;
            try
            {
                response = JsonUtility.FromJson<Response>(body ?? string.Empty);
            }
            catch (Exception)
            {
                return Unreadable(model);
            }

            string content = response?.message?.content;
            if (string.IsNullOrWhiteSpace(content))
                return ReplyCheckResult.Unavailable(model + " gave an empty answer.");

            string answer = content.Trim();
            if (string.Equals(answer, "Yes", StringComparison.OrdinalIgnoreCase)) return ReplyCheckResult.Blocked(risk);
            if (string.Equals(answer, "No", StringComparison.OrdinalIgnoreCase)) return ReplyCheckResult.Safe();

            var scores = Score.Matches(answer);
            if (scores.Count > 0)
            {
                bool yes = false, no = false;
                foreach (Match score in scores)
                {
                    if (string.Equals(score.Groups[1].Value, "yes", StringComparison.OrdinalIgnoreCase)) yes = true;
                    else no = true;
                }
                if (yes && !no) return ReplyCheckResult.Blocked(risk);
                if (no && !yes) return ReplyCheckResult.Safe();
            }
            return Unreadable(model);
        }

        /// <summary>The <c>error</c> field of a JSON error body, or null.</summary>
        private static string ErrorOf(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || body.IndexOf("\"error\"", StringComparison.Ordinal) < 0) return null;
            try
            {
                var response = JsonUtility.FromJson<Response>(body);
                return string.IsNullOrWhiteSpace(response?.error) ? null : response.error;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ReplyCheckResult Unreadable(string model) =>
            ReplyCheckResult.Unavailable(model + " gave an answer the app cannot read (expected Yes or No).");
    }
}
