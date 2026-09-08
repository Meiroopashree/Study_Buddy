namespace StudyBuddy.Models
{
    /// <summary>
    /// Produces per-exam question-type guidance so quiz generation matches the
    /// format of the exam the student is preparing for (e.g. JEE Main vs JEE Advanced
    /// vs CAT vs GMAT). Unknown/custom exams (NEET, BITSAT, ...) fall back to a
    /// sensible default pattern determined by the exam name.
    /// </summary>
    public static class ExamGuidance
    {
        public const string Main = "JEE Main";
        public const string Advanced = "JEE Advanced";
        public const string Cat = "CAT";
        public const string Xat = "XAT";
        public const string Nmat = "NMAT";
        public const string Snap = "SNAP";
        public const string Gmat = "GMAT";
        public const string Gre = "GRE";
        public const string Cmat = "CMAT";
        public const string SscCgl = "SSC CGL";
        public const string Bank = "Bank PO/Clerk";

        public static bool IsAdvanced(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, Advanced, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Advanced", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "JEE Advanced", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMain(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, Main, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Main", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "JEE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "JEE Main", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when the exam is a medical/biology-oriented entrance test (e.g. NEET).</summary>
        public static bool IsMedical(string? exam)
        {
            var e = (exam ?? "").Trim();
            return e.Contains("NEET", StringComparison.OrdinalIgnoreCase)
                || e.Contains("AIIMS", StringComparison.OrdinalIgnoreCase)
                || e.Contains("medical", StringComparison.OrdinalIgnoreCase)
                || e.Contains("MBBS", StringComparison.OrdinalIgnoreCase);
        }

        // ==================== Management entrance exams ====================

        public static bool IsCat(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "CAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Common Admission Test", StringComparison.OrdinalIgnoreCase)
                || e.Contains("CAT ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsXat(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "XAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Xavier Aptitude Test", StringComparison.OrdinalIgnoreCase)
                || e.Contains("XAT ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNmat(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "NMAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "NMIMS Management Aptitude Test", StringComparison.OrdinalIgnoreCase)
                || e.Contains("NMAT ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSnap(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "SNAP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Symbiosis National Aptitude Test", StringComparison.OrdinalIgnoreCase)
                || e.Contains("SNAP ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGmat(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "GMAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Graduate Management Admission Test", StringComparison.OrdinalIgnoreCase)
                || e.Contains("GMAT ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGre(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "GRE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Graduate Record Examination", StringComparison.OrdinalIgnoreCase)
                || e.Contains("GRE ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCmat(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "CMAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "Common Management Admission Test", StringComparison.OrdinalIgnoreCase)
                || e.Contains("CMAT", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSsc(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "SSC CGL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "SSC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, "CGL", StringComparison.OrdinalIgnoreCase)
                || e.Contains("Staff Selection Commission", StringComparison.OrdinalIgnoreCase)
                || e.Contains("SSC CGL", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBank(string? exam)
        {
            var e = (exam ?? "").Trim();
            return string.Equals(e, "Bank PO/Clerk", StringComparison.OrdinalIgnoreCase)
                || e.Contains("Bank PO", StringComparison.OrdinalIgnoreCase)
                || e.Contains("Bank Clerk", StringComparison.OrdinalIgnoreCase)
                || e.Contains("Banking", StringComparison.OrdinalIgnoreCase)
                || e.Contains("IBPS", StringComparison.OrdinalIgnoreCase)
                || e.Contains("SBI", StringComparison.OrdinalIgnoreCase);
        }

        // ==================== Quantitative Comparison detection ====================

        /// <summary>
        /// QC (Quantity A vs Quantity B with the four standard answer choices) exists ONLY
        /// in the GRE Quantitative Comparison sections. Everywhere else a QC-style question
        /// is a format violation (the AI tends to over-use it when it sees "GRE").
        /// </summary>
        public static bool AllowsQuantitativeComparison(string? exam, string? subject, string? chapter)
        {
            if (!IsGre(exam)) return false;
            var s = (subject ?? "").Trim();
            var c = (chapter ?? "").Trim();
            return (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("Quant", StringComparison.OrdinalIgnoreCase))
                && (c.Contains("Comparison", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("QC", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsQuantitativeComparisonQuestion(string? questionText)
        {
            if (string.IsNullOrWhiteSpace(questionText)) return false;
            var t = questionText.Trim();
            return t.IndexOf("Quantity A:", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Quantity A :", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Quantity A. ", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Quantity B:", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Quantity B :", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Quantity B. ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Deterministic per-section whitelist of question types. The AI prompt asks for
        /// these formats, but a hard filter guarantees no GRE RC question is ever stored
        /// as a "numerical", no AWA question as a "numerical", no GRE Sentence Equivalence
        /// as anything but "multi", etc. Returns null when the section is unknown so
        /// generic rules apply.
        /// </summary>
        public static string[]? SectionAllowedTypes(string? exam, string? subject, string? chapter)
        {
            var s = (subject ?? "").Trim();
            var c = (chapter ?? "").Trim();

            if (IsXat(exam))
            {
                if (s.Contains("Decision", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("Decision", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypePassage };
                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase)
                    && c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypePassage };
                if (s.Contains("General Knowledge", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq };
                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase)
                    && c.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypePassage };
                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq };
                return new[] { QuestionMapper.TypeMcq };
            }

            if (IsNmat(exam) || IsSnap(exam))
            {
                if ((s.Contains("Reading", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("English", StringComparison.OrdinalIgnoreCase))
                    && c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypePassage };
                return new[] { QuestionMapper.TypeMcq };
            }

            // CMAT / SSC CGL / Bank PO & Clerk: entirely MCQ-based papers. Reading
            // Comprehension uses the passage type; Data Interpretation sections may use
            // passage to carry the data display/table; everything else is plain mcq.
            if (IsCmat(exam) || IsSsc(exam) || IsBank(exam))
            {
                if ((s.Contains("English", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("Language", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("Comprehension", StringComparison.OrdinalIgnoreCase))
                    && c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypePassage };
                if (s.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypePassage };
                return new[] { QuestionMapper.TypeMcq };
            }

            if (IsCat(exam))
            {
                if (c.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("DI ", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypePassage };
                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase))
                    return c.Contains("Reading", StringComparison.OrdinalIgnoreCase)
                        ? new[] { QuestionMapper.TypePassage }
                        : new[] { QuestionMapper.TypeMcq };
                return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypeNumerical };
            }

            if (IsGmat(exam))
            {
                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq };
                if (s.Contains("Data Insights", StringComparison.OrdinalIgnoreCase))
                    return c.Contains("Data Sufficiency", StringComparison.OrdinalIgnoreCase)
                        ? new[] { QuestionMapper.TypeMcq }
                        : new[] { QuestionMapper.TypeMcq, QuestionMapper.TypePassage };
                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase))
                    return c.Contains("Reading", StringComparison.OrdinalIgnoreCase)
                        ? new[] { QuestionMapper.TypePassage }
                        : new[] { QuestionMapper.TypeMcq };
            }

            if (IsGre(exam))
            {
                if (s.Contains("Analytical Writing", StringComparison.OrdinalIgnoreCase))
                    return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypePassage };
                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                        return new[] { QuestionMapper.TypePassage, QuestionMapper.TypeMulti };
                    if (c.Contains("Sentence Equivalence", StringComparison.OrdinalIgnoreCase))
                        return new[] { QuestionMapper.TypeMulti };
                    if (c.Contains("Text Completion", StringComparison.OrdinalIgnoreCase))
                        return new[] { QuestionMapper.TypeMcq };
                    return new[] { QuestionMapper.TypeMcq };
                }
                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase) || s.Contains("Quant", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Comparison", StringComparison.OrdinalIgnoreCase)
                        || c.Contains("QC", StringComparison.OrdinalIgnoreCase))
                        return new[] { QuestionMapper.TypeMcq };
                    return new[] { QuestionMapper.TypeMcq, QuestionMapper.TypeNumerical };
                }
            }

            return null;
        }

        public static bool SectionAllowsType(string? exam, string? subject, string? chapter, string type)
        {
            var allowed = SectionAllowedTypes(exam, subject, chapter);
            if (allowed == null) return true;
            return allowed.Contains(type, StringComparer.OrdinalIgnoreCase);
        }

        // ==================== Question types per exam ====================

        public static string[] AllowedTypes(string? exam)
        {
            if (IsAdvanced(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypeMulti,
                    QuestionMapper.TypeNumerical,
                    QuestionMapper.TypeAssertion,
                    QuestionMapper.TypeMatching,
                    QuestionMapper.TypePassage,
                    QuestionMapper.TypeTrueFalse
                };

            // Medical exams (NEET etc.): single-correct MCQ only (no negative-marking numericals).
            if (IsMedical(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypeAssertion,
                    QuestionMapper.TypePassage
                };

            // GMAT: Problem Solving / Critical Reasoning / Sentence Correction / Data
            // Sufficiency as MCQs (DS carried in the passage field) + Reading Comprehension.
            if (IsGmat(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypePassage
                };

            // GRE: single-answer MCQ, multi-select (incl. Sentence Equivalence),
            // numeric entry, and Reading Comprehension.
            if (IsGre(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypeMulti,
                    QuestionMapper.TypeNumerical,
                    QuestionMapper.TypePassage
                };

            // CAT: single-correct MCQs, passage/RC, and TITA (type-in-the-answer).
            if (IsCat(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypeNumerical,
                    QuestionMapper.TypePassage
                };

            // XAT: single-correct MCQs plus passage/caselet-based questions
            // (RC, Decision Making, Analytical Caselets).
            if (IsXat(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypePassage
                };

            // NMAT and SNAP: fully MCQ-based papers (reading comprehension via passage).
            // CMAT, SSC CGL and Bank PO/Clerk are also fully MCQ-based papers with no
            // negative marking: quant, reasoning, general awareness and (for CMAT)
            // innovation/entrepreneurship are all single-correct MCQs, RC via passage.
            if (IsNmat(exam) || IsSnap(exam) || IsCmat(exam) || IsSsc(exam) || IsBank(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypePassage
                };

            // JEE Main: Section A MCQs + Section B numerical value questions.
            if (IsMain(exam))
                return new[]
                {
                    QuestionMapper.TypeMcq,
                    QuestionMapper.TypeNumerical,
                    QuestionMapper.TypePassage
                };

            // Default for any other / custom exam: a balanced mix of MCQs,
            // numericals and passage-based questions.
            return new[]
            {
                QuestionMapper.TypeMcq,
                QuestionMapper.TypeNumerical,
                QuestionMapper.TypePassage
            };
        }

        private static string AllowedDescription(string[] types)
        {
            var map = new Dictionary<string, string>
            {
                [QuestionMapper.TypeMcq] = "\"mcq\": single correct answer, 4 options",
                [QuestionMapper.TypeMulti] = "\"multi\": one or more correct answers, 4 options",
                [QuestionMapper.TypeNumerical] = "\"numerical\": integer or numerical-value answer (no options)",
                [QuestionMapper.TypeAssertion] = "\"assertion\": Assertion-Reason with the two statements plus the four standard choices",
                [QuestionMapper.TypeMatching] = "\"matching\": match List-I items to List-II items (4 + 4)",
                [QuestionMapper.TypePassage] = "\"passage\": a short comprehension passage with a question based on it (passage field holds the passage text)",
                [QuestionMapper.TypeTrueFalse] = "\"truefalse\": a statement the student must mark True or False"
            };
            return string.Join("\n- ", types.Select(t => map.TryGetValue(t, out var d) ? d : $"\"{t}\"")) + "\n";
        }

        /// <summary>Full prompt section describing which types to use and how many of each.</summary>
        public static string TypeMixInstruction(string? exam)
        {
            var types = AllowedTypes(exam);
            var allowed = AllowedDescription(types);

            string distribution;
            if (IsAdvanced(exam))
            {
                distribution = $@"
Recommended distribution across the set (JEE Advanced style):
- 25-30% mcq (single correct)
- 20-25% multi (one or more correct)
- 20-25% numerical (integer / numerical value, no options)
- 10-15% assertion (Assertion-Reason)
- 10-15% matching (match List-I to List-II)
- 10% passage (comprehension paragraph themed around the chapter)
- 0-5% truefalse";
            }
            else if (IsMedical(exam))
            {
                distribution = $@"
Recommended distribution across the set (MCQ-based entrance, e.g. NEET):
- 85-90% mcq (single correct, 4 options) with a strong emphasis on Biology as applicable
- 5-10% assertion (Assertion-Reason, statement-based)
- 0-5% passage (comprehension paragraph)";
            }
            else if (IsGmat(exam))
            {
                distribution = $@"
Recommended distribution across the set (GMAT pattern):
- 45-50% mcq: Problem Solving (each with 5 answer options A-E) drawn from quantitative and verbal concepts
- 15-20% mcq: Critical Reasoning — argument-based questions, single correct answer
- 10-15% mcq: Sentence Correction — put the sentence with the underlined portion in the ""passage"" field and give the 5 standard replacement options
- 10-15% mcq: Data Sufficiency — put the question stem plus Statement (1) and Statement (2) in the ""passage"" field and use exactly these five options: [""Statement (1) ALONE is sufficient but statement (2) alone is not"", ""Statement (2) ALONE is sufficient but statement (1) alone is not"", ""BOTH statements TOGETHER are sufficient but NEITHER ALONE is sufficient"", ""EACH statement ALONE is sufficient"", ""Statements (1) and (2) TOGETHER are NOT sufficient""]
- 10-15% passage: Reading Comprehension — a multi-sentence on-topic passage followed by mcq questions based on it";
            }
            else if (IsGre(exam))
            {
                distribution = $@"
Recommended distribution across the set (GRE pattern):
- 35-40% mcq: Quantitative Comparison — put ""Quantity A: ..."" and ""Quantity B: ..."" in the ""passage"" field and use exactly these four options: [""Quantity A is greater"", ""Quantity B is greater"", ""The two quantities are equal"", ""The relationship cannot be determined from the information given""]
- 15-20% mcq: standard single-answer multiple choice with 5 options (Quant and Verbal)
- 10-15% multi: Select one or more answer choices (mark all that apply)
- 10-15% multi: Sentence Equivalence — student must pick TWO options that produce sentences of equivalent meaning; provide exactly two correct_answers
- 10-15% numerical: Numeric Entry — no options, the computed numeric value (GRE entry box)
- 10-15% passage: Reading Comprehension — a short on-topic passage followed by mcq questions based on it";
            }
            else if (IsCat(exam))
            {
                distribution = $@"
Recommended distribution across the set (CAT pattern):
- 55-60% mcq: single correct MCQs (Quantitative Aptitude, Verbal Ability, Logical Reasoning)
- 20-25% passage: Reading Comprehension passages and Data Interpretation sets — put the RC paragraph or the DI table/graph description in the ""passage"" field and ask a focused mcq based on it; keep each set question self-contained
- 10-15% numerical: TITA (Type In The Answer) — no options, exact numeric/short answer, NO negative marking
- 0-5% multi: multiple-correct logic questions only where the pattern genuinely requires it";
            }
            else if (IsXat(exam))
            {
                distribution = $@"
Recommended distribution across the set (XAT pattern):
- 65-70% mcq: single correct MCQs (Verbal and Logical, Quantitative, General Knowledge)
- 30-35% passage: Reading Comprehension, Decision Making and Analytical Caselets — put the case facts/caselet in the ""passage"" field and ask a scenario-based mcq; XAT Decision Making asks what a decision-maker SHOULD do, so phrase options as plausible management actions";
            }
            else if (IsNmat(exam) || IsSnap(exam))
            {
                distribution = $@"
Recommended distribution across the set (NMAT/SNAP MCQ pattern):
- 80% mcq: single correct MCQs with no negative marking across all sections
- 20% passage: Reading Comprehension paragraphs and Data Sufficiency stems — put the stem or paragraph in the ""passage"" field and ask a focused mcq based on it";
            }
            else if (IsCmat(exam) || IsSsc(exam) || IsBank(exam))
            {
                distribution = $@"
Recommended distribution across the set (MCQ-based competitive paper, no negative marking):
- 80% mcq: single correct MCQs with 4 options across Quantitative Aptitude, Reasoning, English/Language, General Awareness and (where applicable) Innovation/Entrepreneurship
- 20% passage: Reading Comprehension paragraphs and Data Interpretation tables/graphs — put the paragraph or the table/graph description in the ""passage"" field and ask a focused mcq based on it
- Do NOT use ""numerical"", ""multi"", ""assertion"" or ""matching"" anywhere in this paper";
            }
            else
            {
                // JEE Main and the default for any custom exam.
                distribution = $@"
Recommended distribution across the set (Section A MCQs + Section B numerical value):
- 55-65% mcq (single correct, 4 options)
- 30-40% numerical (integer / numerical value, no options, 1-2 in decimal form)
- 5-10% passage (comprehension paragraph with an mcq/numerical based on it)";
            }

            var examLabel = string.IsNullOrWhiteSpace(exam) ? "selected exam" : exam;

            return $@"
5. Use ONLY these question types that match the {examLabel} paper pattern:
- {allowed}
{distribution}

For ""passage"" questions: provide a substantive multi-line passage in the ""passage"" field, and make ""question"" a focused single-correct or numerical question based purely on the passage. Do NOT invent passages for questions that do not need them.
Keep the total number of options realistic for the pattern: use 4 options for standard MCQs unless a section (GMAT Problem Solving / GRE single-answer) explicitly calls for 5.
";
        }

        /// <summary>
        /// Section-aware question-format directive for a specific subject/chapter, so the
        /// generated quiz matches the EXACT format of the section the student is studying
        /// (e.g. GRE Sentence Equivalence must be two-answer "multi", GMAT Data Sufficiency
        /// uses the five standard options, XAT Decision Making must be scenario MCQs).
        /// Returns "" when the section is unknown so the generic distribution applies.
        /// </summary>
        public static string SectionFormatInstruction(string? exam, string? subject, string? chapter)
        {
            var s = (subject ?? "").Trim();
            var c = (chapter ?? "").Trim();

            if (IsXat(exam))
            {
                if (s.Contains("Decision", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("Decision", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (XAT DECISION MAKING): EVERY question MUST be a decision-making scenario. Encode as type ""mcq"": put a complete realistic business/managerial scenario (2-4 sentences of case facts) in the ""passage"" field, make ""question"" the decision that must be made (e.g. ""What should the manager do?""), and give exactly 4 plausible action options in ""options"". The correct answer should be the best defensible action for a responsible manager. No numerical and no non-scenario question is allowed.";

                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase) && c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (XAT VERBAL / READING COMPREHENSION): Use type ""passage"" questions only: a substantive on-topic passage in the ""passage"" field with a single-correct mcq (4 options) based on it. No non-passage questions in this set.";

                if (s.Contains("General Knowledge", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (XAT GENERAL KNOWLEDGE): Every question MUST be a factual single-correct mcq with 4 options and no passage. Ask about the events, institutions and facts defined by the topic. Keep the correct answer unambiguous.";

                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase) && c.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (XAT DATA INTERPRETATION): Encode data sets as type ""passage"" (put the table/graph text in the ""passage"" field) with an mcq (4 options) based purely on it. Where no data display is needed, use a plain mcq. TITA/numerical is NOT used in XAT.";

                return @"
6. SECTION FORMAT (XAT QUANTITATIVE): Use single-correct mcq with 4 options. Do NOT use the ""numerical"" type and do NOT use ""passage"" for pure math.";

            }

            if (IsNmat(exam) || IsSnap(exam))
            {
                if (s.Contains("Reading", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("English", StringComparison.OrdinalIgnoreCase) && c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (READING COMPREHENSION): Every question MUST be type ""passage"": a substantive on-topic passage in ""passage"" plus a single-correct mcq (4 options) based on it.";

                return @"
6. SECTION FORMAT (MCQ-ONLY PAPER): EVERY question MUST be a single-correct mcq with exactly 4 options. Do NOT use ""numerical"", ""multi"", ""assertion"" or ""matching"". For Data Sufficiency stems or Reasoning caselets, put the stem/caselet in the ""passage"" field and keep ""type"" as ""mcq"".";
            }

            if (IsCat(exam))
            {
                if (s.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (CAT DATA INTERPRETATION): Every question MUST be based on a data display. Encode as type ""passage"": put the table/graph/arrangement description in the ""passage"" field and ask a single-correct mcq (4 options) based purely on it. No numerical questions in this set.";

                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (CAT READING COMPREHENSION): Every question MUST be type ""passage"": a substantive passage in ""passage"" with a single-correct mcq (4 options) based on it.";
                    return @"
6. SECTION FORMAT (CAT VERBAL ABILITY): Use single-correct mcq (4 options). Para-jumble questions: give the sentence fragments in a fixed order inside ""options"" as labelled choices and mark the correct arrangement. Do NOT use ""numerical"" or ""passage"" here.";
                }

                if (s.Contains("Logical Reasoning", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (CAT LOGICAL REASONING): Use single-correct mcq (4 options), and ""numerical"" TITA only where the answer is an exact value. Do NOT use ""passage"" for LR grids.";

                return @"
6. SECTION FORMAT (CAT QUANTITATIVE APTITUDE): Use single-correct mcq (4 options) and ""numerical"" (TITA, no options) in roughly 80/20 mix. Do NOT use ""passage"" for pure math.";
            }

            if (IsGmat(exam))
            {
                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (GMAT PROBLEM SOLVING): EVERY question MUST be type ""mcq"" with EXACTLY 5 answer options. Do NOT use ""numerical"", ""passage"" or multi-select in this section.";

                if (s.Contains("Data Insights", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Data Sufficiency", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GMAT DATA SUFFICIENCY): EVERY question MUST be a Data Sufficiency question. Encode as type ""mcq"": put the question stem plus ""Statement (1):"" and ""Statement (2):"" in the ""passage"" field and use EXACTLY these five options: [""Statement (1) ALONE is sufficient but statement (2) alone is not"", ""Statement (2) ALONE is sufficient but statement (1) alone is not"", ""BOTH statements TOGETHER are sufficient but NEITHER ALONE is sufficient"", ""EACH statement ALONE is sufficient"", ""Statements (1) and (2) TOGETHER are NOT sufficient""]. The ""question"" must restate what is being asked. No other type is allowed.";
                    return @"
6. SECTION FORMAT (GMAT DATA INSIGHTS): Each question targets a real data display. Put a textual description of the graph/table/paired data in the ""passage"" field and ask a single-correct mcq with EXACTLY 5 options based on it. For two-part analyses, encode as ""mcq"" whose options are the answer pairs. No ""numerical"" type.";
                }

                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GMAT READING COMPREHENSION): EVERY question MUST be type ""passage"": a substantive passage in ""passage"" with a choice of 5 answer options in ""options"".";
                    return @"
6. SECTION FORMAT (GMAT CRITICAL REASONING): EVERY question MUST be type ""mcq"" with EXACTLY 5 options. Put the full argument inside the ""question"" text (premises and conclusion) — do NOT use the ""passage"" field. Question types: identify the assumption, strengthen, weaken, flaw, evaluate, inference, boldface role.";
                }
            }

            if (IsGre(exam))
            {
                if (s.Contains("Analytical Writing", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Argument", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GRE ARGUMENT ANALYSIS): EVERY question MUST test a flaw/evidence-location skill from the argument in the topic. Encode as type ""mcq"" (4 options) or ""passage"" only when a short sample argument (the facts of the passage) is needed and the question is a single-correct mcq based on it. Questions are about spotting flawed reasoning, unstated assumptions, weakened/strengthened evidence. Do NOT ask students to free-write; all questions must have a single correct mcq answer.";
                    if (c.Contains("Issue", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GRE ISSUE ESSAY STRATEGY): MCQ-only format (4 options) testing how to build, structure and support a position on an issue prompt. No ""passage"" and no free-writing questions.";
                    return @"
6. SECTION FORMAT (GRE WRITING MECHANICS): MCQ-only format (4 options) testing grammar, punctuation and sentence clarity. No ""passage"" and no free-writing questions.";
                }

                if (s.Contains("Verbal", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GRE READING COMPREHENSION): EVERY question MUST be type ""passage"": a substantive on-topic passage in ""passage"" with a single-answer question and 5 options in ""options"" (use ""multi"" only for the few select-all-that-apply reading questions, with 3-5 options).";
                    if (c.Contains("Sentence Equivalence", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GRE SENTENCE EQUIVALENCE): EVERY question MUST be Sentence Equivalence. Encode as type ""multi"": a single sentence with ONE blank in ""question"", SIX candidate words in ""options"", and EXACTLY TWO correct options (record both exact option texts in ""correct_answers"" and joined by comma in ""correct_answer"") that each complete the sentence with equivalent meaning. Do NOT produce quantitative questions here.";
                    if (c.Contains("Text Completion", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GRE TEXT COMPLETION): EVERY question MUST be Text Completion. Encode as type ""mcq"": a sentence (or 2-3 sentence passage) with exactly ONE blank in ""question"" (write the blank as ""______""), and candidate words only for that blank in ""options"" (5 options). Single correct answer = the exact word. Do NOT produce quantitative questions here.";
                    return @"
6. SECTION FORMAT (GRE VERBAL / VOCABULARY): Use single-answer mcq with 5 options about word meanings, usage and sentence context. No ""passage"" type in this set.";
                }

                // Quantitative Reasoning
                if (s.Contains("Quantitative", StringComparison.OrdinalIgnoreCase))
                {
                    if (c.Contains("Quantitative Comparison", StringComparison.OrdinalIgnoreCase)
                        || c.Contains("QC", StringComparison.OrdinalIgnoreCase)
                        || c.Contains("Comparison", StringComparison.OrdinalIgnoreCase))
                        return @"
6. SECTION FORMAT (GRE QUANTITATIVE COMPARISON): EVERY question MUST be a Quantitative Comparison. Encode as type ""mcq"" with EXACTLY these four options in ""options"": [""Quantity A is greater"", ""Quantity B is greater"", ""The two quantities are equal"", ""The relationship cannot be determined from the information given""]. Write ""Quantity A:"" and ""Quantity B:"" on two separate lines in the ""question"" text (never inside ""passage""). Do NOT use the ""numerical"" or ""passage"" types here.";
                    return @"
6. SECTION FORMAT (GRE QUANTITATIVE REASONING): Use standard GRE Quant formats: single-answer mcq with EXACTLY 5 options, ""numerical"" (no options) when the exact value is asked, and Quantitative Comparison only where comparing is the natural question (with the four standard QC options). Do NOT use the ""passage"" type for math.";
                }
            }

            if (IsCmat(exam) || IsSsc(exam) || IsBank(exam))
            {
                if ((s.Contains("English", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("Language", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("Comprehension", StringComparison.OrdinalIgnoreCase))
                    && c.Contains("Reading", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (READING COMPREHENSION): EVERY question MUST be type ""passage"": a substantive on-topic passage in ""passage"" plus a single-correct mcq (4 options) based on it. No non-passage question is allowed in this set.";

                if (s.Contains("General Awareness", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (GENERAL AWARENESS): Every question MUST be a factual single-correct mcq with 4 options and no passage. Ask about the events, institutions, banking/economy facts and static GK defined by the topic. Keep the correct answer unambiguous.";

                if (s.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("Data Interpretation", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (DATA INTERPRETATION): Encode data sets as type ""passage"" (put the table/graph description in the ""passage"" field) with a single-correct mcq (4 options) based purely on it. Where no data display is needed, use a plain mcq. TITA/numerical is NOT used.";

                if (s.Contains("Innovation", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("Entrepreneurship", StringComparison.OrdinalIgnoreCase))
                    return @"
6. SECTION FORMAT (INNOVATION & ENTREPRENEURSHIP): Every question MUST be a scenario/factual single-correct mcq with 4 options. For case-based questions put a short business/startup scenario in the ""passage"" field and ask a single-correct mcq based on it. No ""numerical"" type.";

                return @"
6. SECTION FORMAT (MCQ-ONLY PAPER): EVERY question MUST be a single-correct mcq with exactly 4 options. Do NOT use ""numerical"", ""multi"", ""assertion"" or ""matching"". For Data Sufficiency stems or Reasoning caselets/puzzles, put the stem or caselet in the ""passage"" field and keep ""type"" as ""mcq"".";
            }

            return "";
        }

        /// <summary>
        /// Exam-aware teaching context injected into topic/lesson generation, so study
        /// material is pitched at the depth and style of the target exam.
        /// </summary>
        public static string ExamContextInstruction(string? exam)
        {
            if (string.IsNullOrWhiteSpace(exam))
                return "EXAM CONTEXT: This is a general topic with no specific exam tag. Present a well-rounded, balanced lesson covering fundamentals with examples.";

            if (IsAdvanced(exam))
                return "EXAM CONTEXT: This topic belongs to the JEE Advanced syllabus. Tailor the material to JEE Advanced depth -- require thorough derivations, subtle conceptual traps, edge cases, and connections across topics. Include higher-order application examples. Do NOT dilute the content for board-level or Main-level ease. Emphasize properties, proofs and multi-step reasoning.";

            if (IsGmat(exam))
                return "EXAM CONTEXT: This topic belongs to the GMAT syllabus. Focus on the Quantitative and Verbal skills GMAT tests: conceptual clarity, fast and accurate computation WITHOUT a calculator, and behave like a business-problem critical thinker. Cover typical GMAT traps (extreme numbers, reversed fractions, 'could vs. must be true') and show time-saving shortcuts. Note how the concept appears in Problem Solving, Data Sufficiency, Critical Reasoning, Sentence Correction and Reading Comprehension questions.";

            if (IsGre(exam))
                return "EXAM CONTEXT: This topic belongs to the GRE syllabus. Cover the concept with the depth GRE Quant and Verbal require: clear definitions, standard formulas, reasoning about relative quantities (Quantitative Comparison), and vocabulary-in-context for verbal. Include GRE-style numeric-entry and multi-select practice and common traps.";

            if (IsCat(exam))
                return "EXAM CONTEXT: This topic belongs to the CAT syllabus. Teach with an aptitude-exam mindset: concept clarity plus speed. Show shortcut/approximation techniques for Quantitative Aptitude, how the concept is turned into Data Interpretation and Logical Reasoning questions, and reading strategies for Verbal Ability. Include typical CAT trap options and the fastest solving path.";

            if (IsXat(exam))
                return "EXAM CONTEXT: This topic belongs to the XAT syllabus. Teach for XAT's breadth: Quantitative Aptitude, Verbal and Logical, General Knowledge, and Decision Making. Give analytical reasoning practice and caselet-based thinking. Highlight how this concept appears in XAT's adaptive, no-heavy-calculator sections and its unique negative-marking-for-skipping rule.";

            if (IsNmat(exam))
                return "EXAM CONTEXT: This topic belongs to the NMAT syllabus. Cover Language Skills, Logical Reasoning and Quantitative Skills at a fast, no-negative-marking, all-MCQ pace. Emphasize speed and accuracy without a calculator, and show how the concept appears in NMAT's adaptive tests.";

            if (IsSnap(exam))
                return "EXAM CONTEXT: This topic belongs to the SNAP syllabus. Cover General English, Quantitative & Data Interpretation & Data Sufficiency, and Analytical & Logical Reasoning. Emphasize quick, accurate MCQ solving with no negative marking and strong reasoning shortcuts.";

            if (IsCmat(exam))
                return "EXAM CONTEXT: This topic belongs to the CMAT syllabus. CMAT is a fully MCQ-based management entrance (no negative marking). Cover Quantitative Techniques, Logical Reasoning, Language Comprehension, Data Interpretation, General Awareness, and Innovation & Entrepreneurship at a speed-oriented, accurate-all-MCQ pace. Give caselets and management-thinking examples where the topic allows, and highlight exam traps.";

            if (IsSsc(exam))
                return "EXAM CONTEXT: This topic belongs to the SSC CGL syllabus. Teach at a fast, accurate, all-MCQ pace across Quantitative Aptitude, General Intelligence & Reasoning, English Comprehension, and General Awareness. Emphasize shortcuts, formula recall, quick elimination of distractors, and speed under time pressure. Keep concepts aligned to standard government-exam conventions.";

            if (IsBank(exam))
                return "EXAM CONTEXT: This topic belongs to the Bank PO/Clerk syllabus. Cover Quantitative Aptitude, Reasoning Ability, English Language, and General/Banking Awareness at the fast, all-MCQ pace of bank exams. Emphasize calculation shortcuts, puzzle/caselet solving strategy, reading speed for RC/cloze, and banking & economy awareness. Highlight common trap options and time-management per question.";

            // Custom/unknown exam names keep the previous generic depth (this preserves
            // the historical default that used JEE Main-level tailoring).
            return "EXAM CONTEXT: This topic belongs to the " + exam + " syllabus. Tailor the material to JEE Main depth -- focus on clear conceptual understanding, direct application of standard formulas, and typical exam-level practice. Keep notation and definitions aligned to NCERT, and highlight the most commonly tested points. Avoid over-extending into topics exclusive to JEE Advanced.";
        }
    }
}