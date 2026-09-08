import React, { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import remarkGfm from "remark-gfm";
import "katex/dist/katex.min.css";
import {
  getAdaptiveInsights, getAdaptivePlan, getAdaptiveMistakes,
  getAdaptiveQuiz, getAdaptiveDrill, saveQuizResult
} from "../services/api";
import formatAIText from "../utils/formatAIText";
import { isQuizAnswerCorrect, formatQuizAnswer } from "../components/QuizQuestionBlock";
import "../styles/Learn.css";
import "../styles/Adaptive.css";

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

function classificationClass(classification) {
  if (classification === "weak") return "badge-weak";
  if (classification === "needs-work") return "badge-needs-work";
  return "badge-strong";
}

function classificationLabel(classification) {
  if (classification === "weak") return "Weak";
  if (classification === "needs-work") return "Needs work";
  return "Strong";
}

function formatDay(dateValue) {
  if (!dateValue) return "";
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const date = new Date(dateValue);
  date.setHours(0, 0, 0, 0);
  const diff = Math.round((today - date) / 86400000);
  if (diff === 0) return "Today";
  if (diff === 1) return "Yesterday";
  return date.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
}

function groupMistakes(mistakes) {
  const dayMap = new Map();
  mistakes.forEach((m) => {
    const key = new Date(m.completedAt).toDateString();
    if (!dayMap.has(key)) {
      dayMap.set(key, { key, label: formatDay(m.completedAt), subjects: new Map() });
    }
    const day = dayMap.get(key);
    const subject = m.subject || "Uncategorized";
    if (!day.subjects.has(subject)) day.subjects.set(subject, { subject, topics: new Map() });
    const subj = day.subjects.get(subject);
    const topic = m.topic || "General";
    if (!subj.topics.has(topic)) subj.topics.set(topic, { topic, items: [] });
    subj.topics.get(topic).items.push(m);
  });
  return Array.from(dayMap.values())
    .sort((a, b) => (a.key < b.key ? 1 : -1))
    .map((d) => ({
      key: d.key,
      label: d.label,
      subjects: Array.from(d.subjects.values())
        .sort((a, b) => (a.subject.toLowerCase() < b.subject.toLowerCase() ? -1 : 1))
        .map((s) => ({ subject: s.subject, topics: Array.from(s.topics.values()) })),
    }));
}

function Adaptive() {
  const [insights, setInsights] = useState(null);
  const [plan, setPlan] = useState(null);
  const [mistakesData, setMistakesData] = useState(null);
  const [errors, setErrors] = useState({ insights: "", plan: "", mistakes: "" });
  const [loading, setLoading] = useState(true);

  const [collapsedDays, setCollapsedDays] = useState(() => new Set());
  const [collapsedSubjects, setCollapsedSubjects] = useState(() => new Set());
  const [collapsedTopics, setCollapsedTopics] = useState(() => new Set());

  const toggleDay = (key) => {
    setCollapsedDays((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  };

  const toggleSubject = (key) => {
    setCollapsedSubjects((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  };

  const toggleTopic = (key) => {
    setCollapsedTopics((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  };

  const [quizModal, setQuizModal] = useState(false);
  const [quizTopic, setQuizTopic] = useState(null);
  const [quizCount, setQuizCount] = useState(10);
  const [quiz, setQuiz] = useState(null);
  const [quizLoading, setQuizLoading] = useState(false);
  const [quizError, setQuizError] = useState("");
  const [quizAnswers, setQuizAnswers] = useState({});
  const [quizSubmitted, setQuizSubmitted] = useState(false);
  const [quizScore, setQuizScore] = useState(0);
  const quizStartRef = useRef(null);

  const loadAll = async () => {
    setLoading(true);
    setErrors({ insights: "", plan: "", mistakes: "" });
    const loadOne = async (key, fn, setter) => {
      try {
        const data = await fn();
        setter(data);
        return "";
      } catch (e) {
        return e.message || "Failed to load";
      }
    };
    const [insightsErr, planErr, mistakesErr] = await Promise.all([
      loadOne("insights", getAdaptiveInsights, setInsights),
      loadOne("plan", getAdaptivePlan, setPlan),
      loadOne("mistakes", getAdaptiveMistakes, setMistakesData),
    ]);
    setErrors({ insights: insightsErr, plan: planErr, mistakes: mistakesErr });
    setLoading(false);
  };

  useEffect(() => {
    loadAll();
  }, []);

  const openQuiz = (topic, count = 10) => {
    setQuizTopic(topic);
    setQuizCount(count);
    setQuizModal(true);
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
    setQuizError("");
  };

  const closeQuiz = () => {
    setQuizModal(false);
    setQuizTopic(null);
    setQuiz(null);
  };

  const startQuiz = async () => {
    if (!quizTopic) return;
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
    setQuizError("");
    setQuizLoading(true);
    try {
      const data = await getAdaptiveQuiz(quizTopic, quizCount);
      setQuiz(data);
      quizStartRef.current = Date.now();
    } catch (e) {
      setQuizError(e.message);
    } finally {
      setQuizLoading(false);
    }
  };

  const startDrill = async () => {
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
    setQuizError("");
    setQuizLoading(true);
    setQuizTopic("Weak Topic Drill");
    setQuizModal(true);
    try {
      const data = await getAdaptiveDrill(quizCount);
      setQuiz({ topic: "Weak Topic Drill", questions: data.questions, drillTopics: data.topics || [] });
      quizStartRef.current = Date.now();
    } catch (e) {
      setQuizError(e.message);
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
      topic: quiz.topic,
      difficulty: "adaptive",
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
    setTimeout(() => loadAll(), 300);
  };

  const summary = insights?.summary || {};

  const renderPlan = () => {
    if (!plan || !plan.hasData) return null;
    const recommended = plan.recommendedQuiz;
    return (
      <section className="adaptive-section adaptive-plan">
        <h2>Today's Plan</h2>
        {recommended ? (
          <div className="adaptive-plan-recommend">
            <div>
              <p className="muted">Recommended: <strong>{recommended.topic}</strong></p>
              <p className="muted">
                {recommended.mistakeCount} mistakes · {recommended.attempts} attempt{recommended.attempts === 1 ? "" : "s"}
                {recommended.hasQuestions ? " · practice questions ready" : ""}
              </p>
            </div>
            <button className="btn btn-primary" onClick={() => openQuiz(recommended.topic, 10)}>
              Start Recommended Quiz
            </button>
            <button
              className="btn btn-success"
              onClick={startDrill}
              title="Automatically build a quiz from your weakest topics"
            >
              Drill My Weak Topics
            </button>
          </div>
        ) : (
          <p className="muted">No topics need revision right now. Great work!</p>
        )}
        {plan.revisionTopics && plan.revisionTopics.length > 0 && (
          <div className="adaptive-revision-list">
            {plan.revisionTopics.map((t, i) => (
              <div className="adaptive-revision-row" key={`${t.topic}-${i}`}>
                <span className={`badge ${classificationClass(t.classification)}`}>
                  {classificationLabel(t.classification)}
                </span>
                <span className="adaptive-revision-topic">{t.topic}</span>
                <span className="muted">
                  {t.daysSinceLast === 0 ? "last studied today" : `${t.daysSinceLast}d ago`}
                </span>
                {t.hasQuestions && (
                  <button className="btn btn-sm" onClick={() => openQuiz(t.topic, 10)}>
                    Practice
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
      </section>
    );
  };

  const renderTopics = () => {
    if (!insights || !insights.hasData) return null;
    const topics = insights.topics || [];
    if (topics.length === 0) return null;
    return (
      <section className="adaptive-section">
        <h2>Topic Insights</h2>
        <div className="adaptive-topics-table">
          <div className="adaptive-topics-head">
            <span>Topic</span>
            <span>Status</span>
            <span>Confidence</span>
            <span>Accuracy</span>
            <span>Attempts</span>
            <span>Last studied</span>
          </div>
          {topics.map((t, i) => (
            <div className="adaptive-topics-row" key={`${t.topic}-${i}`}>
              <span className="adaptive-topic-name">{t.topic}</span>
              <span>
                <span className={`badge ${classificationClass(t.classification)}`}>
                  {classificationLabel(t.classification)}
                </span>
              </span>
              <span>
                <div className="confidence-bar">
                  <div className="confidence-fill" style={{ width: `${Math.min(t.confidence, 100)}%` }} />
                </div>
                <span className="muted">{t.confidence}%</span>
              </span>
              <span>{t.accuracy}%</span>
              <span>{t.attempts}</span>
              <span>
                {t.daysSinceLast === 0 ? "Today" : `${t.daysSinceLast}d ago`}
                {t.hasQuestions && (
                  <button className="btn btn-sm adaptive-practice-btn" onClick={() => openQuiz(t.topic, 10)}>
                    Practice
                  </button>
                )}
              </span>
            </div>
          ))}
        </div>
      </section>
    );
  };

  const renderMistakes = () => {
    if (!mistakesData || mistakesData.count === 0) return null;
    const all = mistakesData.mistakes || [];
    if (all.length === 0) return null;
    const groups = groupMistakes(all);
    return (
      <section className="adaptive-section">
        <h2>Mistakes to Review</h2>
        <p className="muted">
          Questions you answered incorrectly, grouped by day, subject and topic — newest first.
        </p>
        <div className="adaptive-mistakes-groups">
          {groups.map((day) => {
            const dayOpen = !collapsedDays.has(day.key);
            const dayTotal = day.subjects.reduce((s, subj) => s + subj.topics.reduce((t, tp) => t + tp.items.length, 0), 0);
            return (
              <div className="adaptive-mistake-day" key={day.key}>
                <button className="adaptive-mistake-day-head" onClick={() => toggleDay(day.key)}>
                  <span className="adaptive-chevron">{dayOpen ? "▾" : "▸"}</span>
                  <span className="adaptive-mistake-day-label">{day.label}</span>
                  <span className="muted">
                    {day.subjects.length} subject{day.subjects.length === 1 ? "" : "s"} · {dayTotal} mistake{dayTotal === 1 ? "" : "s"}
                  </span>
                </button>
                {dayOpen && (
                  <div className="adaptive-mistake-subjects">
                    {day.subjects.map((subj) => {
                      const sKey = `${day.key}::${subj.subject}`;
                      const sOpen = !collapsedSubjects.has(sKey);
                      const sTotal = subj.topics.reduce((t, tp) => t + tp.items.length, 0);
                      return (
                        <div className="adaptive-mistake-subject" key={sKey}>
                          <button className="adaptive-mistake-subject-head" onClick={() => toggleSubject(sKey)}>
                            <span className="adaptive-chevron">{sOpen ? "▾" : "▸"}</span>
                            <span className={`badge badge-subject`}>{subj.subject}</span>
                            <span className="muted">{sTotal} mistake{sTotal === 1 ? "" : "s"}</span>
                          </button>
                          {sOpen && (
                            <div className="adaptive-mistake-topics">
                              {subj.topics.map((t) => {
                                const tKey = `${sKey}::${t.topic}`;
                                const tOpen = !collapsedTopics.has(tKey);
                                return (
                                  <div className="adaptive-mistake-topic" key={tKey}>
                                    <button className="adaptive-mistake-topic-head" onClick={() => toggleTopic(tKey)}>
                                      <span className="adaptive-chevron">{tOpen ? "▾" : "▸"}</span>
                                      <span className="review-topic-tag">{t.topic}</span>
                                      <span className="muted">
                                        {t.items.length} mistake{t.items.length === 1 ? "" : "s"}
                                      </span>
                                    </button>
                                    {tOpen && (
                                      <div className="adaptive-mistake-list">
                                        {t.items.map((m, i) => (
                                          <div className="adaptive-mistake" key={`${m.quizId}-${i}`}>
                                            <Markdown content={`**${m.questionText}**`} />
                                            <div className="quiz-options review-options">
                                              {(m.options || []).map((opt) => (
                                                <div
                                                  key={opt}
                                                  className={`quiz-option ${opt === m.correctAnswer ? "is-correct" : opt === m.yourAnswer ? "is-wrong" : ""}`}
                                                >
                                                  <Markdown content={opt} />
                                                  {opt === m.correctAnswer && <span className="quiz-badge">Correct</span>}
                                                  {opt === m.yourAnswer && opt !== m.correctAnswer && (
                                                    <span className="quiz-badge quiz-badge-wrong">Your answer</span>
                                                  )}
                                                </div>
                                              ))}
                                            </div>
                                            {m.explanation && (
                                              <div className="quiz-explanation">
                                                <Markdown content={`**Explanation:** ${m.explanation}`} />
                                              </div>
                                            )}
                                          </div>
                                        ))}
                                      </div>
                                    )}
                                  </div>
                                );
                              })}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </section>
    );
  };

  return (
    <div className="adaptive-page">
      <header className="adaptive-header">
        <h1>Adaptive Learning</h1>
        <p className="muted">
          StudyBuddy tracks your quiz performance to find weak topics, plan revision,
          and give you a personalised quiz from your mistakes.
        </p>
      </header>

      {Object.values(errors).some((e) => e) && (
        <p className="auth-error adaptive-load-error">
          Some sections couldn't load. Refresh to try again.
        </p>
      )}

      {loading && !insights ? (
        <p className="muted adaptive-loading">Loading your adaptive insights...</p>
      ) : insights && insights.hasData ? (
        <>
          <section className="adaptive-summary">
            <div className="adaptive-card">
              <span className="adaptive-card-value">{summary.quizzesTaken}</span>
              <span className="adaptive-card-label">Quizzes taken</span>
            </div>
            <div className="adaptive-card">
              <span className="adaptive-card-value">{summary.accuracy}%</span>
              <span className="adaptive-card-label">Accuracy</span>
            </div>
            <div className="adaptive-card">
              <span className="adaptive-card-value">{summary.avgTimePerQuestion}s</span>
              <span className="adaptive-card-label">Avg time / question</span>
            </div>
            <div className="adaptive-card">
              <span className="adaptive-card-value">{summary.mistakes}</span>
              <span className="adaptive-card-label">Mistakes</span>
            </div>
            <div className="adaptive-card">
              <span className="adaptive-card-value">{summary.weakTopics}</span>
              <span className="adaptive-card-label">Weak topics</span>
            </div>
            <div className="adaptive-card">
              <span className="adaptive-card-value">{summary.strongTopics}</span>
              <span className="adaptive-card-label">Strong topics</span>
            </div>
          </section>

          {plan && plan.hasData ? renderPlan() : errors.plan && (
            <section className="adaptive-section adaptive-plan">
              <h2>Today's Plan</h2>
              <p className="muted">Your plan couldn't be loaded right now. Refresh to try again.</p>
            </section>
          )}
          {renderTopics()}
          {renderMistakes()}
        </>
      ) : (
        <div className="learn-empty adaptive-empty">
          <h2>No data yet</h2>
          <p className="muted">
            Take a few quizzes on the Learn page or a Question Paper quiz, and this
            page will build your personalised study plan.
          </p>
        </div>
      )}

      {quizModal && quizTopic && (
        <div className="modal-overlay" onClick={closeQuiz}>
          <div className="modal-card papers-quiz-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Adaptive Quiz: {quizTopic}</h3>
              <button className="close-btn" onClick={closeQuiz} aria-label="Close">×</button>
            </div>

            {quizError && <p className="auth-error quiz-config-error">{quizError}</p>}

            {!quiz ? (
              <div className="quiz-config">
                <label>
                  Number of questions
                  <select value={quizCount} onChange={(e) => setQuizCount(Number(e.target.value))}>
                    {[5, 10, 15, 20, 30, 40].map((n) => <option key={n} value={n}>{n}</option>)}
                  </select>
                </label>
                <p className="muted quiz-config-hint">
                  Prioritises questions you got wrong, then fills with stored questions for this topic.
                </p>
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
                  </div>
                </div>
                <div className="quiz-body">
                  {quiz.questions.map((q, i) => (
                    <div
                      className={`quiz-question ${quizAnswers[i] === q.answer ? "is-correct" : "is-wrong"}`}
                      key={q.id ?? i}
                    >
                      <Markdown content={`**Q${i + 1}.** ${q.questionText}`} />
                      <div className="quiz-options">
                        {(q.options || []).map((opt) => (
                          <div
                            key={opt}
                            className={`quiz-option ${opt === q.answer ? "is-correct" : quizAnswers[i] === opt ? "is-wrong" : ""}`}
                          >
                            <Markdown content={opt} />
                            {opt === q.answer && <span className="quiz-badge">Correct</span>}
                            {quizAnswers[i] === opt && opt !== q.answer && <span className="quiz-badge quiz-badge-wrong">Your answer</span>}
                          </div>
                        ))}
                      </div>
                      {q.explanation && (
                        <div className="quiz-explanation">
                          <Markdown content={`**Explanation:** ${q.explanation}`} />
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <>
                <div className="quiz-body">
                  {quiz.questions.map((q, i) => (
                    <div className="quiz-question" key={q.id ?? i}>
                      <Markdown content={`**Q${i + 1}.** ${q.questionText}`} />
                      <div className="quiz-options">
                        {(q.options || []).map((opt) => (
                          <label key={opt} className="quiz-option">
                            <input
                              type="radio"
                              name={`q${i}`}
                              checked={quizAnswers[i] === opt}
                              onChange={() => setQuizAnswers((prev) => ({ ...prev, [i]: opt }))}
                            />
                            <Markdown content={opt} />
                          </label>
                        ))}
                      </div>
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

export default Adaptive;
