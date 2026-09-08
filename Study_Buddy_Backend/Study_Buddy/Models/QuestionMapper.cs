using System.Text.Json;

namespace StudyBuddy.Models
{
    public static class QuestionMapper
    {
        public const string TypeMcq = "mcq";
        public const string TypeMulti = "multi";
        public const string TypeNumerical = "numerical";
        public const string TypeAssertion = "assertion";
        public const string TypeMatching = "matching";
        public const string TypeTrueFalse = "truefalse";
        public const string TypePassage = "passage";

        public static readonly string[] SupportedTypes =
            { TypeMcq, TypeMulti, TypeNumerical, TypeAssertion, TypeMatching, TypeTrueFalse, TypePassage };

        public static string NormalizeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return TypeMcq;
            var t = type.Trim().ToLowerInvariant();
            return t switch
            {
                "mcq" or "single" or "single_select" or "single_correct" or "single_choice" => TypeMcq,
                "multi" or "multi_select" or "multiple" or "multiple_correct" or "msq" => TypeMulti,
                "numerical" or "numeric" or "integer" or "integer_type" or "integer_answer"
                    or "numerical_value" or "numerical_answer" or "na" or "short" => TypeNumerical,
                "assertion" or "assertion_reason" or "assertion-reason" or "assertionreason" => TypeAssertion,
                "matching" or "match" or "match_the_following" or "match_the_following_2" or "matching_lists" => TypeMatching,
                "truefalse" or "true_false" or "true-false" or "boolean" or "tf" => TypeTrueFalse,
                "passage" or "comprehension" or "passage_based" or "passagebased" or "paragraph"
                    or "passage_comprehension" or "matrix" => TypePassage,
                _ => TypeMcq
            };
        }

        public static List<string> CorrectAnswers(RawQuestion q, string type)
        {
            if (type != TypeMulti) return new List<string>();
            if (q.correct_answers != null && q.correct_answers.Count > 0)
                return q.correct_answers.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
            if (!string.IsNullOrWhiteSpace(q.correct_answer))
                return q.correct_answer.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
            return new List<string>();
        }

        public static Question ToQuestion(RawQuestion q)
        {
            var type = NormalizeType(q.type);
            var options = q.options ?? new List<string>();
            var answers = CorrectAnswers(q, type);

            return new Question
            {
                Type = type,
                QuestionText = q.question ?? "",
                Options = type == TypeNumerical ? null : options,
                Answer = type == TypeMulti
                    ? string.Join(", ", answers)
                    : (q.correct_answer ?? ""),
                CorrectAnswers = type == TypeMulti ? answers : null,
                Assertion = q.assertion,
                Reason = q.reason,
                RightOptions = type == TypeMatching ? q.right_options : null,
                Passage = q.passage,
                Explanation = q.explanation ?? ""
            };
        }

        public static string AnswersJson(IEnumerable<string>? correctAnswers, string type)
        {
            if (type != TypeMulti) return "[]";
            var list = correctAnswers?
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .ToList() ?? new List<string>();
            return JsonSerializer.Serialize(list);
        }

        public static string AnswersJson(RawQuestion q, string type) => AnswersJson(q.correct_answers, type);

        public static string RightOptionsJson(IEnumerable<string>? rightOptions, string type)
        {
            if (type != TypeMatching) return "[]";
            return JsonSerializer.Serialize(rightOptions?.ToList() ?? new List<string>());
        }

        public static string RightOptionsJson(RawQuestion q, string type) => RightOptionsJson(q.right_options, type);

        /// <summary>
        /// Checks that a raw question has the fields its declared type actually needs,
        /// so malformed/AI-hallucinated questions never reach the student.
        /// </summary>
        public static bool HasValidFormat(RawQuestion q)
        {
            if (q == null) return false;
            if (string.IsNullOrWhiteSpace(q.question)) return false;
            if (string.IsNullOrWhiteSpace(q.correct_answer)) return false;

            var type = NormalizeType(q.type);
            if (type == TypeMulti)
            {
                // must have at least one correct answer and at least 2 options to be meaningful
                if (CorrectAnswers(q, type).Count < 1) return false;
                return (q.options?.Count ?? 0) >= 2;
            }
            if (type == TypeMatching)
            {
                // must have List-I (options) and List-II (right_options)
                return (q.options?.Count ?? 0) >= 2 && (q.right_options?.Count ?? 0) >= 2
                    && !string.IsNullOrWhiteSpace(q.correct_answer);
            }
            if (type == TypeAssertion)
            {
                return !string.IsNullOrWhiteSpace(q.assertion) && !string.IsNullOrWhiteSpace(q.reason);
            }
            if (type == TypePassage)
            {
                // a comprehension question needs the passage paragraph plus a plausible option set
                return !string.IsNullOrWhiteSpace(q.passage) && (q.options?.Count ?? 0) >= 2;
            }
            if (type == TypeNumerical)
            {
                // numerical has no options; the answer must be a number
                return System.Text.RegularExpressions.Regex.IsMatch(q.correct_answer.Trim(), @"^[-+]?[0-9]*\.?[0-9]+$");
            }

            // mcq / truefalse
            return (q.options?.Count ?? 0) >= 2;
        }
    }
}
