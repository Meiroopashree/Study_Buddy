using StudyBuddy.Interfaces;
using StudyBuddy.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StudyBuddy.Services
{
    public class MistralService : IMistralService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public MistralService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClient = httpClientFactory.CreateClient();

            _httpClient.BaseAddress = new Uri("https://api.mistral.ai/v1/");
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["Mistral:ApiKey"]);
        }

        public async Task<string> AskAI(string question, List<ChatMessage>? history = null, string? documentContext = null, string? imageUrl = null, string? mode = null)
        {
            var (messages, model) = BuildMessages(question, history, documentContext, imageUrl, mode);
            return await SendChatRequest(messages, model);
        }

        public async Task<Stream> AskAIStream(string question, List<ChatMessage>? history = null, string? documentContext = null, string? imageUrl = null, string? mode = null)
        {
            var (messages, model) = BuildMessages(question, history, documentContext, imageUrl, mode);
            return await SendStreamRequest(messages, model);
        }

        public async Task<string> SendRawPrompt(string prompt)
        {
            var messages = new List<object>
            {
                new { role = "user", content = prompt }
            };
            return await SendChatRequest(messages, "ministral-14b-2512", 16384);
        }

        public async Task<string> TranscribeImage(string imageDataUri)
        {
            var messages = new List<object>
            {
                new { role = "user", content = new object[]
                    {
                        new { type = "text", text = "Transcribe ALL text from this image as accurately and completely as possible. Preserve paragraph structure, headings, and list formatting. If the image contains mathematical expressions, rewrite them using LaTeX notation. Return ONLY the transcribed text with no extra commentary." },
                        new { type = "image_url", image_url = new { url = imageDataUri } }
                    }
                }
            };
            return await SendChatRequest(messages, "ministral-14b-2512", 16384);
        }

        private (List<object> Messages, string Model) BuildMessages(string question, List<ChatMessage>? history, string? documentContext = null, string? imageUrl = null, string? mode = null)
        {
            var messages = new List<object>();
            var model = "ministral-14b-2512";

            var effectiveQuestion = string.IsNullOrWhiteSpace(question)
                ? "What can you tell me about this image?"
                : question;

            var systemPrompt = @"You are an expert teacher and explainer. Provide a clear, structured, and easy-to-understand explanation suitable for students.

Output in Markdown format. Use ## headings for sections and ### for subsections.

For ANY mathematical expressions, fractions, equations, or variables, ALWAYS wrap them in \(...\) for inline math or \[...\] for display math.
- Inline example: \(ax^2 + bx + c = 0\)
- Display example: \[\frac{{-b \pm \sqrt{{b^2 - 4ac}}}}{{2a}}\]
- Put \(...\) or \[...\] around EVERY equation and around standalone commands such as \boxed{{...}}, \frac{{a}}{{b}}, \sqrt{{x}}, \text{{...}} and \quad. NEVER write bare LaTeX like \boxed{{x}} or \frac{{1}}{{2}} outside the \(...\) / \[...\] delimiters.

For step-by-step solutions, present each step as a separate paragraph. Use \[...\] display math for equations that need to stand on their own line. Align equations properly inside the math delimiters.

CRITICAL: Do NOT use asterisks (** or any variant) for bold or emphasis. Do NOT use any symbol-based formatting outside of Markdown headings and the math delimiters described above. Rely on section structure and plain text for clarity.

When the user asks about a concept, asks you to explain an answer, or asks why something is correct, teach like a great tutor. Use ## headings and include every section that applies:
- Step-by-Step: numbered steps that build the full solution or explanation.
- Diagram: when a drawing helps (circuit, forces, ray diagram, graph, geometry, biological structure), add a simple text diagram inside a fenced code block.
- Formula Breakdown: for every formula, explain what each symbol means and its units.
- Common Mistakes: the typical errors students make and how to avoid them.
- Related Concepts: what the student should already know, and what connects next.
- Follow-Up Practice: end with one short practice question for the student, then an Answer heading giving the solution.";

            var modeInstructions = GetModeInstructions(mode);
            if (!string.IsNullOrWhiteSpace(modeInstructions))
                systemPrompt += "\n\n" + modeInstructions;

            if (!string.IsNullOrWhiteSpace(documentContext))
            {
                systemPrompt += $"\n\nThe user has provided the following document for context. Use it to answer questions:\n\n---DOCUMENT START---\n{documentContext}\n---DOCUMENT END---";
            }

            messages.Add(new { role = "system", content = systemPrompt });

            if (history != null)
            {
                foreach (var msg in history)
                {
                    if (!string.IsNullOrWhiteSpace(msg.Question))
                        messages.Add(new { role = "user", content = msg.Question });
                    if (!string.IsNullOrWhiteSpace(msg.Response))
                        messages.Add(new { role = "assistant", content = msg.Response });
                }
            }

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                model = "ministral-14b-2512";
                var contentParts = new List<object>
                {
                    new { type = "text", text = effectiveQuestion },
                    new { type = "image_url", image_url = new { url = imageUrl } }
                };
                messages.Add(new { role = "user", content = contentParts });
            }
            else
            {
                messages.Add(new { role = "user", content = effectiveQuestion });
            }

            return (messages, model);
        }

        private static string GetModeInstructions(string? mode)
        {
            return mode?.Trim().ToLowerInvariant() switch
            {
                "beginner" => @"TUTOR MODE: BEGINNER
- Assume the student is new to this topic. Teach from the absolute basics before answering.
- Use simple, friendly language and everyday analogies.
- Define every technical term the first time you use it.
- Break each idea into the smallest possible steps.
- Keep depth modest and end with one easy practice question.",
                "ncert" => @"TUTOR MODE: NCERT (CBSE BOARD)
- Align every explanation with the NCERT curriculum and standard CBSE terminology.
- State the standard NCERT definition before explaining it.
- Keep difficulty at board-exam level and include one solved example.
- Flag the points that commonly appear in board exams.",
                "jeeadvanced" => @"TUTOR MODE: JEE ADVANCED
- Teach with depth and rigor: build intuition first, then justify with a derivation.
- Cover advanced applications, edge cases, and links between concepts.
- Point out the common traps and the fastest way to solve.
- Add an 'Exam angle' note on how this topic is typically asked in JEE Advanced.",
                "neet" => @"TUTOR MODE: NEET
- Focus on NCERT facts across Biology, Physics and Chemistry with direct recall.
- Give memory points and mark the lines worth memorising.
- Teach MCQ elimination techniques and common distractor traps.
- Keep answers precise, exam-relevant and quickly readable.",
                "cat" => @"TUTOR MODE: CAT
- Teach aptitude-style: build conceptual clarity first, then show the fastest solving path and approximation tricks (Quantitative Aptitude).
- Discuss how the concept appears in Data Interpretation and Logical Reasoning sets.
- Add an 'Exam angle' note on time per question and common trap options.
- Give one practice question that mirrors CAT's single-correct or TITA style.",
                "xat" => @"TUTOR MODE: XAT
- Teach for XAT's breadth: Quantitative Aptitude, Verbal and Logical, General Knowledge and Decision Making.
- Add an 'Exam angle' note on Decision Making questions and XAT's negative marking for skipped questions.
- Show analytical/caselet reasoning and how the concept is tested without heavy calculation.",
                "nmat" => @"TUTOR MODE: NMAT
- Focus on fast, accurate, all-MCQ solving with no negative marking (Language Skills, Logical Reasoning, Quantitative Skills).
- Show the quickest way to compute each answer and common traps in an adaptive test.",
                "snap" => @"TUTOR MODE: SNAP
- Emphasize quick MCQ solving (General English, Quantitative & DI & DS, Analytical & Logical Reasoning) with no negative marking.
- Give shortcuts and reasoning methods, and end with one SNAP-style practice question.",
                "gmat" => @"TUTOR MODE: GMAT
- Teach quantitative and verbal the way the GMAT tests them: business reasoning without a calculator.
- Add an 'Exam angle' note for Problem Solving (5 options), Data Sufficiency, Critical Reasoning, Sentence Correction and Reading Comprehension.
- Include an 'Exam angle' note on time management and common score-killing traps.",
                "gre" => @"TUTOR MODE: GRE
- Teach Quant and Verbal at GRE depth: standard formulas, reasoning about relative quantities, numeric entry and multi-select.
- Add an 'Exam angle' note on Quantitative Comparison and vocabulary-in-context.
- Give one GRE-style practice question.",
                "cmat" => @"TUTOR MODE: CMAT
- Teach each section (Quantitative Techniques, Logical Reasoning, Language Comprehension, Data Interpretation, General Awareness, Innovation & Entrepreneurship) as a fully MCQ-based, no-negative-marking paper.
- Emphasize speed and accuracy, and add an 'Exam angle' note on how the concept appears in CMAT (all-MCQ, quick caselets).",
                "ssc" => @"TUTOR MODE: SSC CGL
- Teach Quantitative Aptitude, General Intelligence & Reasoning, English Comprehension and General Awareness at a fast all-MCQ pace.
- Emphasize shortcuts, quick elimination of distractor options and formula recall; add an 'Exam angle' note on how the topic is asked in SSC CGL.",
                "bank" => @"TUTOR MODE: BANK PO/CLERK
- Teach Quantitative Aptitude, Reasoning Ability, English Language and Banking/General Awareness at the fast all-MCQ pace of bank exams.
- Show calculation shortcuts, puzzle/caselet strategies and RC/cloze technique; add an 'Exam angle' note on trap options and time per question.",
                "revision" => @"TUTOR MODE: REVISION
- Be concise and fast-paced. Use bullet points and short sections only.
- Lead with the key formula or definition, then the few points that matter most.
- Add a 'Do not forget' line with the most commonly missed detail.
- End with one quick recall question for the student.",
                _ => ""
            };
        }

        private async Task<Stream> SendStreamRequest(List<object> messages, string model)
        {
            var requestBody = new
            {
                model = model,
                messages = messages,
                stream = true
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = content };
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        private async Task<string> SendChatRequest(List<object> messages, string model, int? maxTokens = null)
        {
            var requestBody = maxTokens.HasValue
                ? new
                {
                    model = model,
                    messages = messages,
                    max_tokens = maxTokens.Value
                }
                : (object)new
                {
                    model = model,
                    messages = messages
                };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Error: {response.StatusCode}, {error}";
            }

            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);

            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString() ?? "No response from AI";
        }
    }
}
