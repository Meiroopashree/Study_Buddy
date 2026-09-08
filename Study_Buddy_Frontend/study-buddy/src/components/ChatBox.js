import React, { useState, useRef, useEffect } from "react";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import "katex/dist/katex.min.css";
import Message from "./Message";
import QuizQuestionBlock, { isQuizAnswerCorrect, formatQuizAnswer } from "./QuizQuestionBlock";
import { askAI, askAIStream, generateQuiz, saveQuizResult, getQuizHistory, getQuizDetail, uploadImage, getDocuments, uploadDocument, deleteDocument, createNote, ocrDocument, getLearningTree } from "../services/api";
import formatAIText from "../utils/formatAIText";
import { useExam } from "../contexts/ExamContext";
import "../styles/ChatBox.css";

const TIMER_MINUTES = 10;

const TUTOR_MODES = [
  { key: "Beginner", label: "Beginner", hint: "Teach from the basics" },
  { key: "NCERT", label: "NCERT", hint: "Board-level, standard definitions" },
  { key: "JEE Advanced", label: "JEE Advanced", hint: "Depth, derivations, traps" },
  { key: "NEET", label: "NEET", hint: "Facts, memory points, elimination" },
  { key: "Revision", label: "Revision", hint: "Quick, concise review" },
];

function ChatBox() {
  const { currentExam } = useExam();
  const [input, setInput] = useState("");
  const [messages, setMessages] = useState([]);
  const [conversationHistory, setConversationHistory] = useState([]);
  const [quizTopic, setQuizTopic] = useState("");
  const [quiz, setQuiz] = useState(null);
  const [difficulty, setDifficulty] = useState("medium");
  const [questionCount, setQuestionCount] = useState(5);
  const [quizExam, setQuizExam] = useState("JEE Advanced");
  const [examOptions, setExamOptions] = useState(["JEE Advanced", "JEE Main"]);
  const [answers, setAnswers] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);
  const [quizHistory, setQuizHistory] = useState([]);
  const [showHistory, setShowHistory] = useState(false);
  const [selectedHistoryQuiz, setSelectedHistoryQuiz] = useState(null);
  const [flashcardMode, setFlashcardMode] = useState(false);
  const [currentCard, setCurrentCard] = useState(0);
  const [timeLeft, setTimeLeft] = useState(null);
  const [uploadedImage, setUploadedImage] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [listening, setListening] = useState(false);
  const [documents, setDocuments] = useState([]);
  const [selectedDocIds, setSelectedDocIds] = useState([]);
  const [showDocManager, setShowDocManager] = useState(false);
  const [uploadingDoc, setUploadingDoc] = useState(false);
  const [tutorMode, setTutorMode] = useState(() => localStorage.getItem("sb-tutor-mode") || "NCERT");
  const [toast, setToast] = useState(null);
  const toastTimerRef = useRef(null);
  const fileInputRef = useRef(null);
  const docFileInputRef = useRef(null);
  const ocrFileInputRef = useRef(null);
  const quizStartRef = useRef(null);
  const messagesEndRef = useRef(null);
  const timerRef = useRef(null);
  const recognitionRef = useRef(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, quiz]);

  useEffect(() => {
    if (timeLeft === 0) {
      handleSubmitQuiz();
    }
    if (timeLeft === null || timeLeft <= 0) return;
    timerRef.current = setTimeout(() => setTimeLeft(t => t - 1), 1000);
    return () => clearTimeout(timerRef.current);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [timeLeft]);

  useEffect(() => {
    return () => {
      recognitionRef.current?.stop();
    };
  }, []);

  useEffect(() => {
    localStorage.setItem("sb-tutor-mode", tutorMode);
  }, [tutorMode]);

  useEffect(() => {
    getLearningTree()
      .then((tree) => {
        if (Array.isArray(tree) && tree.length > 0) {
          const names = tree.map((g) => g.exam).filter(Boolean);
          setExamOptions([...new Set(names)]);
          setQuizExam((prev) => (names.includes(prev) ? prev : names[0]));
        }
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (currentExam && examOptions.includes(currentExam)) {
      setQuizExam(currentExam);
    }
  }, [currentExam, examOptions]);

  const addMessage = (text, type) => {
    setMessages((prev) => [...prev, { text, type }]);
  };

  const showToast = (text, type = "success") => {
    if (toastTimerRef.current) clearTimeout(toastTimerRef.current);
    setToast({ text, type });
    toastTimerRef.current = setTimeout(() => setToast(null), 3500);
  };

  const handleAsk = async () => {
    if (!input.trim() && !uploadedImage) return;
    const imageUrl = uploadedImage;
    const userMessage = input.trim()
      ? input
      : "What can you tell me about this image?";
    const displayText = input.trim()
      ? input + (imageUrl ? "\n\n📷 *Image attached*" : "")
      : "📷 *(Image uploaded)*";
    addMessage(displayText, "user");
    setInput("");
    setUploadedImage(null);

    setMessages(prev => [...prev, { text: "", type: "ai", streaming: true }]);

    let fullAnswer = "";
    const docIds = selectedDocIds.length > 0 ? selectedDocIds : undefined;

    const tryStream = async () => {
      await askAIStream(userMessage, conversationHistory,
        (chunk) => {
          fullAnswer += chunk;
          setMessages(prev => {
            const copy = [...prev];
            copy[copy.length - 1] = { text: fullAnswer, type: "ai", streaming: false };
            return copy;
          });
        },
        () => {
          setMessages(prev => {
            const copy = [...prev];
            copy[copy.length - 1] = { text: fullAnswer, type: "ai", streaming: false };
            return copy;
          });
          setConversationHistory(prev => [
            ...prev,
            { role: "user", content: userMessage + (imageUrl ? `\n[Image: ${imageUrl}]` : "") },
            { role: "assistant", content: fullAnswer }
          ]);
        },
        docIds,
        imageUrl,
        tutorMode
      );
    };

    try {
      await tryStream();
    } catch (err) {
      try {
        const res = await askAI(userMessage, conversationHistory, docIds, imageUrl, tutorMode);
        fullAnswer = res.answer || "No response.";
        setMessages(prev => {
          const copy = [...prev];
          copy[copy.length - 1] = { text: fullAnswer, type: "ai", streaming: false };
          return copy;
        });
        setConversationHistory(prev => [
          ...prev,
          { role: "user", content: userMessage + (imageUrl ? `\n[Image: ${imageUrl}]` : "") },
          { role: "assistant", content: fullAnswer }
        ]);
      } catch {
        setMessages(prev => {
          const copy = [...prev];
          copy[copy.length - 1] = { text: "Error fetching response.", type: "ai", streaming: false };
          return copy;
        });
      }
    }
  };

  const handleGenerateQuiz = async () => {
    if (!quizTopic.trim()) {
      addMessage("Please type a topic to generate a quiz.", "ai");
      return;
    }
    setLoading(true);
    try {
      const quizData = await generateQuiz(quizTopic, difficulty, questionCount, quizExam);
      if (quizData && quizData.questions?.length > 0) {
        setQuiz(quizData);
        setAnswers({});
        setSubmitted(false);
        setFlashcardMode(false);
        setCurrentCard(0);
        setTimeLeft(TIMER_MINUTES * 60);
        quizStartRef.current = Date.now();
        addMessage(`Quiz generated for topic: ${quizTopic} (${difficulty})`, "ai");
      } else {
        addMessage("AI returned no quiz questions.", "ai");
      }
    } catch (err) {
      console.error(err);
      addMessage("Error generating quiz.", "ai");
    }
    setLoading(false);
  };

  const handleVoice = () => {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
      addMessage("Voice input is not supported in this browser. Try Chrome or Edge.", "ai");
      return;
    }

    if (listening && recognitionRef.current) {
      recognitionRef.current.stop();
      return;
    }

    const recognition = new SpeechRecognition();
    recognition.lang = "en-US";
    recognition.continuous = true;
    recognition.interimResults = true;

    recognition.onresult = (e) => {
      const transcript = Array.from(e.results).map(r => r[0].transcript).join("");
      setInput(transcript);
    };
    recognition.onend = () => {
      setListening(false);
      recognitionRef.current = null;
    };
    recognition.onerror = (e) => {
      setListening(false);
      recognitionRef.current = null;
      if (e.error === "not-allowed" || e.error === "service-not-allowed") {
        addMessage("Microphone access was blocked. Allow the mic in your browser settings and try again.", "ai");
      } else if (e.error === "no-speech") {
        addMessage("No speech detected. Please try again.", "ai");
      } else if (e.error === "audio-capture") {
        addMessage("No microphone found on this device.", "ai");
      }
    };

    try {
      recognition.start();
      recognitionRef.current = recognition;
      setListening(true);
    } catch (err) {
      setListening(false);
      addMessage("Could not start voice input.", "ai");
    }
  };

  const loadDocuments = async () => {
    try {
      const data = await getDocuments();
      setDocuments(data);
    } catch { /* ignore */ }
  };

  const handleDocUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setUploadingDoc(true);
    try {
      await uploadDocument(file);
      await loadDocuments();
    } catch { addMessage("Failed to upload document.", "ai"); }
    setUploadingDoc(false);
  };

  const handleDeleteDoc = async (id) => {
    try {
      await deleteDocument(id);
      setDocuments(prev => prev.filter(d => d.id !== id));
      setSelectedDocIds(prev => prev.filter(d => d !== id));
    } catch { /* ignore */ }
  };

  const toggleDoc = (id) => {
    setSelectedDocIds(prev => prev.includes(id) ? prev.filter(d => d !== id) : [...prev, id]);
  };

  const handleImageUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setUploading(true);
    try {
      const data = await uploadImage(file);
      setUploadedImage(data.url);
    } catch (err) {
      addMessage(`Failed to upload image: ${err.message}`, "ai");
    }
    setUploading(false);
  };

  const handleSaveNote = async (index) => {
    const aiMsg = messages[index];
    if (!aiMsg || !aiMsg.text) return;
    const prevUser = [...messages.slice(0, index)].reverse().find((m) => m.type === "user");
    const title = prevUser && prevUser.text
      ? prevUser.text.replace(/[#*_`>]/g, "").replace(/\n/g, " ").trim().slice(0, 50)
      : "Saved note";
    try {
      await createNote(title || "Saved note", aiMsg.text);
      showToast("✅ Saved to your notes!", "success");
      addMessage("📌 Saved to your notes (see Dashboard → Notes).", "ai");
    } catch {
      showToast("❌ Failed to save note.", "error");
      addMessage("Failed to save note.", "ai");
    }
  };

  const buildQuizNote = (topic, questions, answersByIndex) => {
    const clean = (s) => String(s || "").trim();
    let note = `# Quiz: ${clean(topic) || "Untitled"}\n\n`;
    questions.forEach((q, i) => {
      note += `## Q${i + 1}. ${clean(q.questionText)}\n\n`;
      if (Array.isArray(q.options) && q.options.length) {
        q.options.forEach(opt => { note += `- ${clean(opt)}\n`; });
        note += "\n";
      }
      note += `**Answer:** ${clean(q.answer)}\n`;
      if (clean(q.explanation)) note += `**Explanation:** ${clean(q.explanation)}\n`;
      if (answersByIndex && answersByIndex[i] !== undefined) {
        const mark = answersByIndex[i] === "known" ? "" : formatQuizAnswer(questions[i], answersByIndex[i]);
        if (clean(mark)) note += `**Your answer:** ${clean(mark)}\n`;
      }
      note += "\n";
    });
    return note.trim();
  };

  const saveQuizToNotes = async (topic, questions, answersByIndex, successMsg) => {
    try {
      await createNote(`Quiz: ${topic || "Untitled"}`, buildQuizNote(topic, questions, answersByIndex));
      showToast("✅ Quiz saved to your notes!", "success");
      addMessage(successMsg || "📌 Quiz saved to your notes (see Dashboard → Notes).", "ai");
    } catch {
      showToast("❌ Failed to save quiz to notes.", "error");
      addMessage("Failed to save quiz to notes.", "ai");
    }
  };

  const saveLiveQuizToNotes = () => {
    if (!quiz || quiz.questions.length === 0) return;
    saveQuizToNotes(quizTopic, quiz.questions, submitted ? answers : null, "📌 Quiz saved to your notes.");
  };

  const saveHistoryQuizToNotes = async () => {
    const hq = selectedHistoryQuiz;
    if (!hq || !hq.questions || hq.questions.length === 0) return;
    const answersByIndex = {};
    if (Array.isArray(hq.answers)) {
      hq.answers.forEach((a, i) => { answersByIndex[i] = a.yourAnswer || ""; });
    }
    await saveQuizToNotes(hq.topic, hq.questions, answersByIndex, "📌 Quiz saved to your notes.");
  };

  const handleOcrUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setUploadingDoc(true);
    try {
      const res = await ocrDocument(file);
      addMessage(`📄 OCR complete — saved as document "${res.title}".`, "ai");
      await loadDocuments();
    } catch (err) {
      addMessage(`OCR failed: ${err.message}`, "ai");
    }
    setUploadingDoc(false);
  };

  const handleSelectAnswer = (qIndex, answer) => {
    if (submitted) return;
    setAnswers({ ...answers, [qIndex]: answer });
  };

  const handleSubmitQuiz = async () => {
    setSubmitted(true);
    setTimeLeft(null);
    if (timerRef.current) clearTimeout(timerRef.current);

    let score = 0;
    const answerDetails = quiz.questions.map((q, i) => {
      const isCorrect = isQuizAnswerCorrect(q, answers[i]);
      if (isCorrect) score++;
      return {
        question: q.questionText,
        yourAnswer: formatQuizAnswer(q, answers[i]),
        correctAnswer: q.answer,
        isCorrect
      };
    });

    try {
      await saveQuizResult({
        topic: quizTopic,
        difficulty,
        score,
        totalQuestions: quiz.questions.length,
        timeSpentSeconds: quizStartRef.current
          ? Math.round((Date.now() - quizStartRef.current) / 1000)
          : 0,
        answers: answerDetails,
        questions: quiz.questions
      });
    } catch (e) {
      console.error("Failed to save quiz result", e);
    }
  };

  const loadHistory = async () => {
    try {
      const data = await getQuizHistory();
      setQuizHistory(data);
      setShowHistory(true);
      setSelectedHistoryQuiz(null);
    } catch (e) {
      console.error("Failed to load history", e);
    }
  };

  const viewHistoryDetail = async (id) => {
    try {
      const data = await getQuizDetail(id);
      setSelectedHistoryQuiz(data);
    } catch (e) {
      console.error("Failed to load detail", e);
    }
  };

  const formatTime = (s) => {
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${m}:${sec.toString().padStart(2, "0")}`;
  };

  const renderQuiz = () => (
    <div className="quiz-container">
      {timeLeft !== null && timeLeft > 0 && !submitted && (
        <div className={`timer ${timeLeft < 60 ? "timer-warning" : ""}`}>
          Time left: {formatTime(timeLeft)}
        </div>
      )}
      {flashcardMode ? (
        <div className="flashcard-container">
          <div className="flashcard">
            <div className="flashcard-question">
              <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                {quiz.questions[currentCard].questionText}
              </ReactMarkdown>
            </div>
            {submitted && (
              <div className="flashcard-answer">
                <strong>Answer:</strong> {quiz.questions[currentCard].answer}
                {quiz.questions[currentCard].explanation && (
                  <div className="explanation">
                    <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                      {formatAIText(quiz.questions[currentCard].explanation)}
                    </ReactMarkdown>
                  </div>
                )}
              </div>
            )}
          </div>
          <div className="flashcard-nav">
            <button onClick={() => setCurrentCard(c => Math.max(0, c - 1))} disabled={currentCard === 0}>
              Previous
            </button>
            <span>{currentCard + 1} / {quiz.questions.length}</span>
            <button onClick={() => setCurrentCard(c => Math.min(quiz.questions.length - 1, c + 1))} disabled={currentCard === quiz.questions.length - 1}>
              Next
            </button>
          </div>
          {!submitted && (
            <div className="flashcard-actions">
              <button onClick={() => { setAnswers({ ...answers, [currentCard]: "known" }); if (currentCard < quiz.questions.length - 1) setCurrentCard(c => c + 1); }}>
                I Know
              </button>
              <button onClick={() => { if (currentCard < quiz.questions.length - 1) setCurrentCard(c => c + 1); }}>
                I Don't Know
              </button>
            </div>
          )}
        </div>
      ) : (
        quiz.questions.map((q, i) => (
          <div key={i} className="question-block">
            <div className="question-text">
              <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                {`${i + 1}. ${q.questionText}`}
              </ReactMarkdown>
            </div>
            <QuizQuestionBlock
              question={q}
              answer={answers[i]}
              submitted={submitted}
              onAnswerChange={(v) => handleSelectAnswer(i, v)}
            />
          </div>
        ))
      )}
      {flashcardMode && submitted && (
        <div className="quiz-score">Score: {Object.values(answers).filter(v => v === "known").length} / {quiz.questions.length} known</div>
      )}
      {!submitted && !flashcardMode && (
        <button className="submit-quiz-btn" onClick={handleSubmitQuiz}>Submit Quiz</button>
      )}
      {!submitted && (
        <button className="toggle-mode-btn" onClick={() => { setFlashcardMode(!flashcardMode); setCurrentCard(0); }}>
          Switch to {flashcardMode ? "Quiz" : "Flashcard"} Mode
        </button>
      )}
      <button className="toggle-mode-btn" onClick={saveLiveQuizToNotes}>
        📌 Save Quiz to Notes
      </button>
    </div>
  );

  const renderHistory = () => (
    <div className="history-container">
      <div className="history-head">
        <h3>Quiz History</h3>
        <button className="close-btn" onClick={() => { setShowHistory(false); setSelectedHistoryQuiz(null); }}>Close</button>
      </div>
      {selectedHistoryQuiz ? (        <div className="history-detail">
          <p><strong>Topic:</strong> {selectedHistoryQuiz.topic}</p>
          <p><strong>Score:</strong> {selectedHistoryQuiz.score} / {selectedHistoryQuiz.totalQuestions}</p>
          <p><strong>Completed:</strong> {new Date(selectedHistoryQuiz.completedAt).toLocaleString()}</p>
          <button className="btn btn-success" onClick={saveHistoryQuizToNotes}
            disabled={!selectedHistoryQuiz.questions || selectedHistoryQuiz.questions.length === 0}>
            📌 Save to Notes
          </button>
          {selectedHistoryQuiz.answers?.map((a, i) => (
            <div key={i} className={`history-answer ${a.isCorrect ? "correct" : "wrong"}`}>
              <p><strong>{i + 1}.</strong> {a.question}</p>
              <p>Your answer: {a.yourAnswer || "(none)"} {a.isCorrect ? "✓" : "✗"}</p>
              {!a.isCorrect && <p>Correct answer: {a.correctAnswer}</p>}
            </div>
          ))}
        </div>
      ) : (
        quizHistory.length === 0 ? <p>No quiz history yet.</p> :
        quizHistory.map((h) => (
          <div key={h.id} className="history-item" onClick={() => viewHistoryDetail(h.id)}>
            <span><strong>{h.topic}</strong> ({h.difficulty})</span>
            <span>{h.score}/{h.totalQuestions}</span>
            <span className="history-date">{new Date(h.completedAt).toLocaleDateString()}</span>
          </div>
        ))
      )}
    </div>
  );

  return (
    <div className="chat-container">
      <h1 className="chat-header">AI Study Buddy <span className="chat-header-mode">· {tutorMode}</span></h1>

      <div className="messages">
        {messages.map((msg, i) => (
          <Message key={i} type={msg.type} text={msg.text} streaming={msg.streaming}
            onSaveNote={msg.type === "ai" && msg.text ? () => handleSaveNote(i) : null} />
        ))}

        {quiz && renderQuiz()}
        {showHistory && renderHistory()}

        {loading && <div className="loading">AI is thinking...</div>}
        <div ref={messagesEndRef} />
      </div>

      <div className="input-section">
        <div className="input-row">
          <textarea value={input} onChange={(e) => setInput(e.target.value)}
            placeholder="Type your question... (Shift+Enter for new line)"
            className="input-box"
            onKeyDown={(e) => e.key === "Enter" && !e.shiftKey && (e.preventDefault(), handleAsk())} />
          <button className={`voice-btn ${listening ? "listening" : ""}`} onClick={handleVoice} title="Voice input">
            {listening ? "🔴" : "🎤"}
          </button>
          <input type="file" accept="image/*" ref={fileInputRef} style={{ display: "none" }}
            onChange={handleImageUpload} />
          <button className="upload-btn" onClick={() => fileInputRef.current?.click()} disabled={uploading} title="Upload image">
            {uploading ? "⏳" : "🖼️"}
          </button>
          <button className="btn btn-primary" onClick={handleAsk}>
            Ask
          </button>
        </div>

        {uploadedImage && (
          <div className="image-preview">
            <img src={uploadedImage} alt="Uploaded preview" />
            <span>Image ready — will be sent with your question</span>
            <button className="btn btn-ghost" onClick={() => setUploadedImage(null)}>Remove</button>
          </div>
        )}

        <div className="tutor-mode-row">
          <span className="tutor-mode-label">Tutor mode</span>
          <div className="tutor-mode-chips">
            {TUTOR_MODES.map((m) => (
              <button
                key={m.key}
                className={`tutor-mode-chip ${tutorMode === m.key ? "active" : ""}`}
                onClick={() => setTutorMode(m.key)}
                title={m.hint}
              >
                {m.label}
              </button>
            ))}
          </div>
        </div>

        <div className="quiz-row">
          <input type="text" value={quizTopic} onChange={(e) => setQuizTopic(e.target.value)}
            placeholder="Enter topic for quiz..." />
          <select value={quizExam} onChange={(e) => setQuizExam(e.target.value)} className="difficulty-dropdown">
            {examOptions.map((ex) => (
              <option key={ex} value={ex}>{ex} pattern</option>
            ))}
          </select>
          <select value={difficulty} onChange={(e) => setDifficulty(e.target.value)} className="difficulty-dropdown">
            <option value="easy">Easy</option>
            <option value="medium">Medium</option>
            <option value="hard">Hard</option>
          </select>
          <select value={questionCount} onChange={(e) => setQuestionCount(Number(e.target.value))} className="difficulty-dropdown">
            <option value={5}>5 questions</option>
            <option value={10}>10 questions</option>
          </select>
          <button className="btn btn-success" onClick={handleGenerateQuiz} disabled={loading || !quizTopic.trim()}>
            Generate Quiz
          </button>
        </div>

        <div className="toolbar-row">
          <button className="btn btn-secondary" onClick={loadHistory}>History</button>
          <button className="btn btn-purple" onClick={() => { loadDocuments(); setShowDocManager(!showDocManager); }}>
            {showDocManager ? "Docs" : `Docs${selectedDocIds.length ? ` (${selectedDocIds.length})` : ""}`}
          </button>
          {messages.length > 0 && (
            <button className="btn btn-danger" onClick={() => window.print()}>PDF</button>
          )}
          {(messages.length > 0 || conversationHistory.length > 0) && (
            <button className="btn btn-ghost" onClick={() => { setConversationHistory([]); setMessages([]); }}>Clear Context</button>
          )}
        </div>
      </div>

      {showDocManager && (
        <div className="doc-manager" style={{ margin: "0 20px 12px" }}>
          <h4>
            My Documents
            <button className="btn btn-success" style={{ padding: "4px 12px", fontSize: "0.8rem" }}
              onClick={() => docFileInputRef.current?.click()} disabled={uploadingDoc}>
              {uploadingDoc ? "..." : "+ Upload"}
            </button>
            <button className="btn btn-purple" style={{ padding: "4px 12px", fontSize: "0.8rem", marginLeft: 6 }}
              onClick={() => ocrFileInputRef.current?.click()} disabled={uploadingDoc}
              title="Extract text from an image (OCR)">
              {uploadingDoc ? "..." : "OCR Image"}
            </button>
          </h4>
          <input type="file" accept=".txt,.md,.pdf,.docx" ref={docFileInputRef} style={{ display: "none" }}
            onChange={handleDocUpload} />
          <input type="file" accept="image/*" ref={ocrFileInputRef} style={{ display: "none" }}
            onChange={handleOcrUpload} />
          {documents.length === 0 ? <p className="doc-empty">No documents yet.</p> :
            documents.map(doc => (
              <div key={doc.id} className="doc-item">
                <label>
                  <input type="checkbox" checked={selectedDocIds.includes(doc.id)}
                    onChange={() => toggleDoc(doc.id)} />
                  <span>{doc.title}</span>
                </label>
                <button className="doc-delete" onClick={() => handleDeleteDoc(doc.id)}>✕</button>
              </div>
            ))
          }
        </div>
      )}

      {toast && (
        <div className={`toast ${toast.type}`} role="status">
          {toast.text}
        </div>
      )}
    </div>
  );
}

export default ChatBox;