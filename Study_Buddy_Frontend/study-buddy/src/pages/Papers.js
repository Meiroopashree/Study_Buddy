import React, { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import remarkGfm from "remark-gfm";
import "katex/dist/katex.min.css";
import LoadingSpinner from "../components/LoadingSpinner";
import {
  getPapers, uploadPaper, getPaperQuiz, deletePaper, saveQuizResult,
  setPaperVisibility, browsePapers, getPaperFilters
} from "../services/api";
import formatAIText from "../utils/formatAIText";
import { downloadAsPdf, downloadTextFile } from "../utils/exportContent";
import QuizQuestionBlock, { isQuizAnswerCorrect, formatQuizAnswer } from "../components/QuizQuestionBlock";
import "../styles/Learn.css";
import "../styles/Papers.css";

function Markdown({ content }) {
  if (!content || !content.trim()) return null;
  return (
    <div className="markdown-body">
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[rehypeKatex]}
      >
        {formatAIText(content)}
      </ReactMarkdown>
    </div>
  );
}

function countOptions(total) {
  if (!total || total <= 0) return [];
  const options = [];
  for (let n = 10; n < total; n += 10) options.push(n);
  if (!options.includes(total)) options.push(total);
  if (options.length === 0) options.push(total);
  return options;
}

function Papers() {
  const [papers, setPapers] = useState([]);
  const [error, setError] = useState("");
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState("");

  const [quizModal, setQuizModal] = useState(false);
  const [quizPaper, setQuizPaper] = useState(null);
  const [quizMode, setQuizMode] = useState("selection");
  const [quizCount, setQuizCount] = useState(10);
  const [quiz, setQuiz] = useState(null);
  const [quizLoading, setQuizLoading] = useState(false);
  const [quizAnswers, setQuizAnswers] = useState({});
  const [quizSubmitted, setQuizSubmitted] = useState(false);
  const [quizScore, setQuizScore] = useState(0);
  const quizStartRef = useRef(null);

  const [uploadExam, setUploadExam] = useState("");
  const [uploadYear, setUploadYear] = useState("");

  const [showBrowse, setShowBrowse] = useState(false);
  const [sharedPapers, setSharedPapers] = useState([]);
  const [browseExam, setBrowseExam] = useState("");
  const [browseYear, setBrowseYear] = useState("");
  const [browseFilters, setBrowseFilters] = useState({ exams: [], years: [] });
  const [browseLoading, setBrowseLoading] = useState(false);
  const [browseBusy, setBrowseBusy] = useState(false);

  const loadPapers = async () => {
    try {
      const data = await getPapers();
      setPapers(data || []);
    } catch (e) {
      setError(e.message);
    }
  };

  useEffect(() => {
    loadPapers();
  }, []);

  useEffect(() => {
    const hasParsing = papers.some((p) => p.status === "parsing");
    if (!hasParsing) return undefined;
    const t = setInterval(loadPapers, 3000);
    return () => clearInterval(t);
  }, [papers]);

  const handleUpload = async (file) => {
    if (!file) return;
    setUploading(true);
    setError("");
    setUploadProgress("Uploading file...");
    try {
      await uploadPaper(file, uploadExam || null, uploadYear ? Number(uploadYear) : null);
      await loadPapers();
      setUploadExam("");
      setUploadYear("");
    } catch (e) {
      setError(e.message);
    } finally {
      setUploading(false);
      setUploadProgress("");
    }
  };

  const handleToggleVisibility = async (p) => {
    setBrowseBusy(true);
    try {
      await setPaperVisibility(p.id, !p.isPublic);
      await loadPapers();
      if (showBrowse) await loadShared();
      const data = await getPaperFilters();
      setBrowseFilters(data);
    } catch (e) {
      setError(e.message);
    } finally {
      setBrowseBusy(false);
    }
  };

  const toggleBrowse = async () => {
    const next = !showBrowse;
    setShowBrowse(next);
    if (next) await loadShared();
  };

  const loadShared = async () => {
    setBrowseLoading(true);
    setError("");
    try {
      const [data, filters] = await Promise.all([
        browsePapers(browseExam || null, browseYear ? Number(browseYear) : null),
        getPaperFilters(),
      ]);
      setSharedPapers(data || []);
      setBrowseFilters(filters);
    } catch (e) {
      setError(e.message);
    } finally {
      setBrowseLoading(false);
    }
  };

  const openSharedQuiz = (id) => {
    if (!id) return;
    const target = sharedPapers.find((p) => p.id === id);
    const paper = target
      ? { id: id, title: target.title, status: "ready", totalQuestions: target.totalQuestions }
      : { id: id, title: "Shared Paper", status: "ready", totalQuestions: 0 };
    setQuizPaper(paper);
    setQuizModal(true);
    setQuizMode("selection");
    setQuizCount(10);
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
  };

  const buildPaperQuizMarkdown = () => {
    if (!quiz) return "";
    const lines = [`# Quiz: ${quiz.paperTitle || quizPaper?.title || "Question Paper"}`, ""];
    quiz.questions.forEach((q, i) => {
      lines.push(`**Q${q.number || i + 1}.** ${formatQuizAnswer(q, undefined).trim()}`);
      lines.push("");
      const yourAnswer = formatQuizAnswer(q, quizAnswers[i]);
      if (quizSubmitted || yourAnswer) {
        lines.push(`- Your answer: ${yourAnswer || "—"}`);
        lines.push(`- Result: ${isQuizAnswerCorrect(q, quizAnswers[i]) ? "Correct" : "Incorrect"}`);
        lines.push("");
      }
    });
    return lines.join("\n");
  };

  const exportPaperQuizPdf = () => {
    if (!quiz) return;
    downloadAsPdf({
      title: `Quiz: ${quiz.paperTitle || quizPaper?.title || "Question Paper"}`,
      subtitle: `${quiz.questions.length} questions · StudyBuddy`,
      markdown: buildPaperQuizMarkdown(),
    });
  };

  const exportPaperQuizMd = () => {
    if (!quiz) return;
    const name = (quiz.paperTitle || quizPaper?.title || "paper").replace(/[^a-z0-9]+/gi, "-").toLowerCase();
    downloadTextFile(`${name}-quiz.md`, buildPaperQuizMarkdown());
  };

  const handleDelete = async (id) => {
    if (!window.confirm("Delete this question paper and its questions?")) return;
    try {
      await deletePaper(id);
      await loadPapers();
    } catch (e) {
      setError(e.message);
    }
  };

  const openQuiz = (paper) => {
    if (paper.status !== "ready") return;
    setQuizPaper(paper);
    setQuizModal(true);
    setQuizMode("selection");
    setQuizCount(10);
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
  };

  const closeQuiz = () => {
    setQuizModal(false);
    setQuizPaper(null);
    setQuiz(null);
  };

  const startQuiz = async () => {
    if (!quizPaper) return;
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
    setQuizLoading(true);
    try {
      const count = quizMode === "selection" ? quizCount : 0;
      const data = await getPaperQuiz(quizPaper.id, count);
      setQuiz(data);
      quizStartRef.current = Date.now();
    } catch (e) {
      setError(e.message);
    } finally {
      setQuizLoading(false);
    }
  };

  const retakeQuiz = () => {
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
    quizStartRef.current = Date.now();
  };

  const newQuiz = () => {
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
  };

  const submitQuiz = () => {
    if (!quiz) return;
    let score = 0;
    quiz.questions.forEach((q, i) => {
      if (isQuizAnswerCorrect(q, quizAnswers[i])) score++;
    });
    setQuizScore(score);
    setQuizSubmitted(true);
    const result = {
      topic: `${quiz.paperTitle} (Question Paper)`,
      difficulty: quiz.mode === "full" ? "full paper" : "selection",
      score,
      totalQuestions: quiz.questions.length,
      timeSpentSeconds: quizStartRef.current ? Math.round((Date.now() - quizStartRef.current) / 1000) : 0,
      answers: quiz.questions.map((q, i) => ({
        question: q.questionText,
        yourAnswer: formatQuizAnswer(q, quizAnswers[i]) || null,
        correctAnswer: q.answer,
        isCorrect: isQuizAnswerCorrect(q, quizAnswers[i]),
      })),
      questions: quiz.questions,
    };
    saveQuizResult(result).catch(() => {});
  };

  const showProgress = (p) => {
    if (p.status === "ready") return `${p.totalQuestions} questions`;
    if (p.status === "failed") return p.errorMessage || "Failed to parse";
    if (p.chunksTotal > 0) return `Parsing... ${Math.min(p.chunksDone, p.chunksTotal)}/${p.chunksTotal}`;
    return "Parsing...";
  };

  return (
    <div className="papers-page">
      <header className="papers-header">
        <h1>Previous Year Question Papers</h1>
        <p className="muted">
          Upload a PDF, DOCX, or image of a question paper. Questions are extracted
          automatically so you can practise them as a quiz.
        </p>
      </header>

      {error && <p className="auth-error">{error}</p>}

      <div className="papers-toolbar">
        <button className="btn btn-ghost" onClick={toggleBrowse}>
          {showBrowse ? "Back to My Papers" : "Browse Shared Papers"}
        </button>
      </div>

      {!showBrowse && (
        <section className="papers-upload">
          <label className={`papers-dropzone ${uploading ? "busy" : ""}`}>
            {uploading ? (
              <LoadingSpinner
                size="inline"
                messages={[
                  `${uploadProgress || "Uploading your paper…"}`,
                  "Extracting text from the paper…",
                  "Splitting into questions…",
                  "Nearly done…",
                ]}
              />
            ) : (
              <>
                <span className="papers-dropzone-title">Choose a question paper</span>
                <span className="muted">Supports .pdf, .docx, .txt and images (.png, .jpg)</span>
                <span className="muted">Extraction may take a minute for large papers</span>
              </>
            )}
            <input
              type="file"
              accept=".pdf,.docx,.txt,.png,.jpg,.jpeg,.gif,.webp,.bmp"
              onChange={(e) => {
                handleUpload(e.target.files[0]);
                e.target.value = "";
              }}
              disabled={uploading}
            />
          </label>
          <div className="papers-upload-meta">
            <label>
              Exam (optional)
              <input
                type="text"
                placeholder="e.g. NEET, JEE Main"
                value={uploadExam}
                onChange={(e) => setUploadExam(e.target.value)}
                disabled={uploading}
              />
            </label>
            <label>
              Year (optional)
              <input
                type="number"
                placeholder="2024"
                min="1990"
                max="2100"
                value={uploadYear}
                onChange={(e) => setUploadYear(e.target.value)}
                disabled={uploading}
              />
            </label>
          </div>
        </section>
      )}

      {showBrowse ? (
        <section className="papers-list papers-shared">
          <div className="papers-shared-filters">
            <label>
              Exam
              <select value={browseExam} onChange={(e) => { setBrowseExam(e.target.value); }}>
                <option value="">All exams</option>
                {browseFilters.exams.map((e) => (
                  <option key={e} value={e}>{e}</option>
                ))}
              </select>
            </label>
            <label>
              Year
              <select value={browseYear} onChange={(e) => { setBrowseYear(e.target.value); }}>
                <option value="">All years</option>
                {browseFilters.years.map((y) => (
                  <option key={y} value={y}>{y}</option>
                ))}
              </select>
            </label>
            <button className="btn btn-primary" onClick={loadShared} disabled={browseLoading}>
              {browseLoading ? "Loading..." : "Apply Filters"}
            </button>
          </div>
          {browseLoading ? (
            <LoadingSpinner
              size="inline"
              message="Loading shared papers…"
            />
          ) : sharedPapers.length === 0 ? (
            <p className="muted">No public papers match these filters yet.</p>
          ) : (
            sharedPapers.map((p) => (
              <div className="paper-card" key={p.id}>
                <div className="paper-card-info">
                  <h3>{p.title}</h3>
                  <p className="muted">
                    <span>{p.exam || "General Exam"}{p.year ? ` · ${p.year}` : ""}</span>
                    <span> · {p.totalQuestions} questions</span>
                    <span> · {new Date(p.createdAt).toLocaleDateString()}</span>
                  </p>
                </div>
                <div className="paper-card-actions">
                  <button className="btn btn-primary" onClick={() => openSharedQuiz(p.id)}>
                    Take Quiz
                  </button>
                </div>
              </div>
            ))
          )}
        </section>
      ) : (
        <section className="papers-list">
          {papers.length === 0 ? (
            <p className="muted">No question papers yet. Upload one above to get started.</p>
          ) : (
            papers.map((p) => (
              <div className="paper-card" key={p.id}>
                <div className="paper-card-info">
                  <h3>{p.title}</h3>
                  <p className="muted">
                    <span className={`paper-status paper-status-${p.status}`}>{showProgress(p)}</span>
                    <span> · {new Date(p.createdAt).toLocaleDateString()}</span>
                    <span>
                      <span className={`paper-status paper-status-${p.isPublic ? "ready" : "private"}`}>
                        {p.isPublic ? "Public" : "Private"}
                      </span>
                    </span>
                    {p.exam && <span> · {p.exam}{p.year ? ` ${p.year}` : ""}</span>}
                  </p>
                </div>
                <div className="paper-card-actions">
                  {p.status === "ready" && (
                    <button className="btn btn-primary" onClick={() => openQuiz(p)}>
                      Take Quiz
                    </button>
                  )}
                  {(p.status === "ready" || p.status === "failed") && (
                    <button
                      className={`btn ${p.isPublic ? "btn-ghost" : "btn-success"}`}
                      onClick={() => handleToggleVisibility(p)}
                      disabled={browseBusy}
                      title={p.isPublic ? "Stop sharing this paper with others" : "Share this paper with everyone (public bank)"}
                    >
                      {p.isPublic ? "Unshare" : "Share"}
                    </button>
                  )}
                  <button className="btn btn-danger-sm" onClick={() => handleDelete(p.id)}>
                    Delete
                  </button>
                </div>
              </div>
            ))
          )}
        </section>
      )}

      {quizModal && quizPaper && (
        <div className="modal-overlay" onClick={closeQuiz}>
          <div className="modal-card papers-quiz-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Quiz: {quizPaper.title}</h3>
              <button className="close-btn" onClick={closeQuiz} aria-label="Close">×</button>
            </div>

            {quizLoading && !quiz ? (
              <LoadingSpinner
                messages={[
                  "Building your paper quiz…",
                  "Reading the question paper…",
                  "Preparing the questions…",
                ]}
              />
            ) : !quiz ? (
              <div className="quiz-config">
                <label>
                  Quiz type
                  <select value={quizMode} onChange={(e) => setQuizMode(e.target.value)}>
                    <option value="selection">Select number of questions</option>
                    <option value="full">Entire paper ({quizPaper.totalQuestions} questions)</option>
                  </select>
                </label>
                {quizMode === "selection" && (
                  <label>
                    Number of questions
                    <select value={quizCount} onChange={(e) => setQuizCount(Number(e.target.value))}>
                      {countOptions(quizPaper.totalQuestions).map((n) => (
                        <option key={n} value={n}>{n}</option>
                      ))}
                    </select>
                  </label>
                )}
                <button className="btn btn-primary" onClick={startQuiz} disabled={quizLoading}>
                  {quizLoading ? "Loading..." : "Start Quiz"}
                </button>
              </div>
            ) : quizSubmitted ? (
              <>
                <div className="quiz-result">
                  <h2>{quizScore} / {quiz.questions.length}</h2>
                  <p className="muted">
                    Accuracy: {Math.round((quizScore / quiz.questions.length) * 100)}%
                  </p>
                  <div className="quiz-result-actions">
                    <button className="btn" onClick={newQuiz}>New Quiz</button>
                    <button className="btn btn-primary" onClick={retakeQuiz}>Retake</button>
                    <button className="btn" onClick={exportPaperQuizPdf} title="Save this quiz as a PDF">PDF</button>
                    <button className="btn" onClick={exportPaperQuizMd} title="Download this quiz as a Markdown file">.md</button>
                  </div>
                </div>
                <div className="quiz-body">
                  {quiz.questions.map((q, i) => (
                    <div
                      className={`quiz-question ${isQuizAnswerCorrect(q, quizAnswers[i]) ? "is-correct" : "is-wrong"}`}
                      key={q.id ?? i}
                    >
                      {q.section && <span className="review-topic-tag">{q.section}</span>}
                      <Markdown content={`**Q${q.number || i + 1}.** ${q.questionText}`} />
                      <QuizQuestionBlock question={q} answer={quizAnswers[i]} submitted />
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <>
                <div className="quiz-body">
                  {quiz.questions.map((q, i) => (
                    <div className="quiz-question" key={q.id ?? i}>
                      {q.section && <span className="review-topic-tag">{q.section}</span>}
                      <Markdown content={`**Q${q.number || i + 1}.** ${q.questionText}`} />
                      <QuizQuestionBlock
                        question={q}
                        answer={quizAnswers[i]}
                        onAnswerChange={(v) => setQuizAnswers((prev) => ({ ...prev, [i]: v }))}
                      />
                    </div>
                  ))}
                </div>
                <div className="modal-footer">
                  <button className="btn btn-primary" onClick={submitQuiz}>
                    Submit Quiz
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default Papers;
