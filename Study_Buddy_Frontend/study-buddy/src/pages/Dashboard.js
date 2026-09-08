import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import "katex/dist/katex.min.css";
import { useAuth } from "../contexts/AuthContext";
import { useExam } from "../contexts/ExamContext";
import { getDashboardStats, createNote, updateNote, deleteNote, getExamStats, getDueReviews } from "../services/api";
import "../styles/Dashboard.css";

function formatDuration(sec) {
  sec = Math.max(0, Math.round(sec || 0));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = sec % 60;
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}

function greeting() {
  const h = new Date().getHours();
  if (h < 12) return "Good morning";
  if (h < 17) return "Good afternoon";
  return "Good evening";
}

function StatCard({ icon, value, label, hint, accent }) {
  return (
    <div className="stat-card" style={{ "--accent": accent }}>
      <span className="stat-icon" aria-hidden="true">{icon}</span>
      <div className="stat-body">
        <span className="stat-value">{value}</span>
        <span className="stat-label">{label}</span>
        {hint && <span className="stat-hint">{hint}</span>}
      </div>
    </div>
  );
}

function EmptyState({ icon, text, action, to }) {
  return (
    <div className="empty-state">
      <span className="empty-icon" aria-hidden="true">{icon}</span>
      <p className="muted">{text}</p>
      {action && (
        <Link to={to} className="btn btn-ghost btn-sm">{action}</Link>
      )}
    </div>
  );
}

function Dashboard() {
  const { user } = useAuth();
  const { currentExam } = useExam();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [noteTitle, setNoteTitle] = useState("");
  const [noteContent, setNoteContent] = useState("");
  const [editingId, setEditingId] = useState(null);
  const [editTitle, setEditTitle] = useState("");
  const [editContent, setEditContent] = useState("");
  const [viewingNote, setViewingNote] = useState(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState(null);
  const [examStats, setExamStats] = useState([]);
  const [dueReviews, setDueReviews] = useState([]);

  const load = async () => {
    try {
      const [d, exams, reviews] = await Promise.all([
        getDashboardStats(),
        getExamStats().catch(() => []),
        getDueReviews().catch(() => []),
      ]);
      setData(d);
      setExamStats(exams);
      setDueReviews(reviews);
      setError("");
    } catch (e) {
      setError(e.message || "Failed to load dashboard");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleCreateNote = async (e) => {
    e.preventDefault();
    if (!noteContent.trim() && !noteTitle.trim()) return;
    try {
      await createNote(noteTitle || "Note", noteContent);
      setNoteTitle("");
      setNoteContent("");
      load();
    } catch (err) {
      setError(err.message);
    }
  };

  const handleUpdateNote = async (id) => {
    try {
      await updateNote(id, editTitle || "Note", editContent);
      setEditingId(null);
      load();
    } catch (err) {
      setError(err.message);
    }
  };

  const handleDeleteNote = async (id) => {
    try {
      await deleteNote(id);
      setDeleteConfirmId(null);
      load();
    } catch (err) {
      setError(err.message);
    }
  };

  const handleDownloadNote = (title, content) => {
    const toPlain = (s) => String(s || "")
      .replace(/[#*_`]/g, "")
      .replace(/\\\(/g, "").replace(/\\\)/g, "")
      .replace(/\\\[/g, "").replace(/\\\]/g, "")
      .replace(/\[/g, "").replace(/\]/g, "")
      .trim();
    const header = toPlain(title) || "Note";
    const body = toPlain(content);
    const text = `${header}\n${"=".repeat(Math.min(60, header.length))}\n\n${body}\n`;
    const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${(title || "note").replace(/[\\/:*?"<>|]/g, "").trim() || "note"}.txt`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  if (loading) {
    return (
      <div className="dashboard-container">
        <div className="skeleton shimmer" style={{ width: "40%", height: 28 }} />
        <div className="stat-cards">
          {[0, 1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="stat-card skeleton shimmer" />
          ))}
        </div>
      </div>
    );
  }

  const a = data?.analytics || {};
  const c = data?.counts || {};
  const uploads = data?.recentUploads || [];
  const quizzes = data?.recentQuizzes || [];
  const notes = data?.recentNotes || [];
  const userName = (user?.username || a?.profile?.username || "Student").split(" ")[0];

  const accuracy = a.accuracy ?? 0;
  const strength = accuracy >= 80 ? "outstanding" : accuracy >= 60 ? "solid" : accuracy > 0 ? "growing" : "";

  return (
    <div className="dashboard-container">
      <header className="dash-hero">
        <div>
          <h1 className="dashboard-title">{greeting()}, {userName} 👋</h1>
          <p className="hero-sub">
            {currentExam
              ? `Focusing on ${currentExam}. `
              : ""}
            {accuracy > 0
              ? `Your quiz accuracy is ${accuracy}% — keep up the ${strength} progress!`
              : "Let's start your study session — take a quiz or explore your syllabus."}
          </p>
        </div>
        <div className="hero-actions">
          <Link to="/" className="btn btn-primary">Ask AI</Link>
          <Link to="/learn" className="btn btn-ghost">Learn</Link>
        </div>
      </header>

      {error && <p className="auth-error dash-error">{error}</p>}

      <div className="stat-cards">
        <StatCard icon="🎯" value={`${accuracy}%`} label="Quiz Accuracy" accent="#0a3339"
          hint={accuracy > 0 ? `Best: ${a.bestScore ?? 0}` : null} />
        <StatCard icon="📝" value={a.quizzesTaken ?? 0} label="Quizzes Taken" accent="#e16526"
          hint={a.avgScore != null ? `Avg score: ${a.avgScore}` : null} />
        <StatCard icon="⏱️" value={formatDuration(a.studyTimeTodaySec)} label="Study Time Today" accent="#16a34a"
          hint={a.avgQuizTimeSec != null ? `Avg quiz: ${formatDuration(a.avgQuizTimeSec)}` : null} />
        <StatCard icon="🗓️" value={formatDuration(a.totalStudyTimeSec)} label="Total Study Time" accent="#d97706" />
        <StatCard icon="📓" value={c.notes ?? 0} label="Notes" accent="#0891b2" />
        <StatCard icon="📄" value={c.documents ?? 0} label="Documents" accent="#dc2626" />
      </div>

      {examStats.length > 0 && (
        <div className="dashboard-panel exam-stats-panel">
          <div className="panel-header">
            <h3>Exam Progress</h3>
            <Link to="/learn" className="panel-more">Open Learn</Link>
          </div>
          <div className="exam-stats-grid">
            {examStats.map((es) => (
              <div className="exam-stat-card" key={es.exam}>
                <div className="exam-stat-head">
                  <span className="exam-stat-name">{es.exam}</span>
                  <span className="exam-stat-pct">{es.pct}%</span>
                </div>
                <div className="exam-stat-bar">
                  <div className="exam-stat-fill" style={{ width: `${es.pct}%` }} />
                </div>
                <div className="exam-stat-detail">
                  <span>{es.mastered} mastered</span>
                  <span>{es.inProgress} in progress</span>
                  <span>{es.revising} revising</span>
                  <span>{es.notStarted} not started</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {dueReviews.length > 0 && (
        <div className="dashboard-panel due-reviews-panel">
          <div className="panel-header">
            <h3>Due for Review</h3>
            <span className="due-count">{dueReviews.length} topic{dueReviews.length === 1 ? "" : "s"}</span>
          </div>
          <ul className="dash-list">
            {dueReviews.slice(0, 8).map((r) => (
              <li key={r.id} className="due-review-row">
                <div className="due-review-info">
                  <span className="due-review-title">{r.topicTitle}</span>
                  <span className="due-review-meta">
                    {r.chapterTitle} · {r.exam}
                    {r.daysOverdue > 0 && <span className="due-overdue"> · {r.daysOverdue}d overdue</span>}
                  </span>
                </div>
                <Link to="/learn" className="btn btn-sm btn-primary">Review</Link>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="quick-actions">
        <p className="quick-title">Quick Actions</p>
        <div className="quick-grid">
          <Link to="/learn" className="quick-card">
            <span className="quick-icon">🧠</span>
            <span>Learn &amp; Practice</span>
            <span className="quick-hint">Syllabus, quizzes &amp; flashcards</span>
          </Link>
          <Link to="/papers" className="quick-card">
            <span className="quick-icon">📄</span>
            <span>Question Papers</span>
            <span className="quick-hint">Upload &amp; practice real papers</span>
          </Link>
          <Link to="/adaptive" className="quick-card">
            <span className="quick-icon">📈</span>
            <span>Adaptive Learning</span>
            <span className="quick-hint">Personalized daily plan</span>
          </Link>
          <Link to="/profile" className="quick-card">
            <span className="quick-icon">👤</span>
            <span>Profile</span>
            <span className="quick-hint">View your stats</span>
          </Link>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="dashboard-panel">
          <div className="panel-header">
            <h3>Recent Quizzes</h3>
            {quizzes.length > 0 && <Link to="/learn" className="panel-more">View all</Link>}
          </div>
          {quizzes.length === 0 ? (
            <EmptyState icon="📝" text="No quizzes yet." action="Take a quiz" to="/learn" />
          ) : (
            <ul className="dash-list">
              {quizzes.map((q) => (
                <li key={q.id} className="quiz-row">
                  <div className="quiz-main">
                    <div className="quiz-title">{q.topic}</div>
                    <div className="quiz-meta">
                      <span className="chip">{q.difficulty}</span>
                      <span className="muted">{new Date(q.completedAt).toLocaleDateString()}</span>
                    </div>
                  </div>
                  <div className="quiz-score">
                    <span className="score-value">{q.score}/{q.totalQuestions}</span>
                    <div className="score-track">
                      <div className="score-fill" style={{ width: `${q.accuracy}%` }} />
                    </div>
                    <span className="score-pct">{q.accuracy}%</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="dashboard-panel">
          <div className="panel-header">
            <h3>Recent Uploads</h3>
            {uploads.length > 0 && <Link to="/papers" className="panel-more">View all</Link>}
          </div>
          {uploads.length === 0 ? (
            <EmptyState icon="📄" text="No uploads yet." action="Upload a paper" to="/papers" />
          ) : (
            <ul className="dash-list">
              {uploads.map((u) => (
                <li key={u.id} className="doc-row">
                  <span className="doc-icon">🗂️</span>
                  <span className="doc-title">{u.title}</span>
                  <span className="muted">{new Date(u.uploadedAt).toLocaleDateString()}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="dashboard-panel notes-panel">
        <div className="panel-header">
          <h3>📓 My Notes</h3>
          {notes.length > 0 && <Link to="/learn" className="panel-more">All notes</Link>}
        </div>
        <form className="note-form" onSubmit={handleCreateNote}>
          <input type="text" placeholder="Title (optional)" value={noteTitle}
            onChange={(e) => setNoteTitle(e.target.value)} />
          <textarea placeholder="Write a note... supports Markdown and LaTeX (e.g. $$E=mc^2$$)"
            value={noteContent} onChange={(e) => setNoteContent(e.target.value)} rows={2} />
          <div className="note-form-footer">
            {noteContent.trim() && <span className="muted">{noteContent.trim().length} chars</span>}
            <button type="submit" className="btn btn-primary" disabled={!noteContent.trim() && !noteTitle.trim()}>
              Add Note
            </button>
          </div>
        </form>

        {notes.length === 0 ? (
          <EmptyState icon="📓" text="No notes yet. Capture your first idea above!" />
        ) : (
          <ul className="dash-list">
            {notes.map((n) => (
              <li key={n.id} className="note-item">
                {editingId === n.id ? (
                  <div className="note-edit">
                    <input type="text" value={editTitle} onChange={(e) => setEditTitle(e.target.value)} />
                    <textarea value={editContent} onChange={(e) => setEditContent(e.target.value)} rows={3} />
                    <div className="note-actions">
                      <button className="btn btn-primary btn-sm" onClick={() => handleUpdateNote(n.id)}>Save</button>
                      <button className="btn btn-ghost btn-sm" onClick={() => setEditingId(null)}>Cancel</button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="note-text">
                      <div className="note-title-row">
                        <strong>{n.title}</strong>
                        <span className="muted">{new Date(n.updatedAt).toLocaleDateString()}</span>
                      </div>
                      {n.content && (
                        <p className="note-preview">{n.content.replace(/[#*_`]/g, "").replace(/[[\]]/g, "").slice(0, 90)}...</p>
                      )}
                    </div>
                    <div className="note-actions">
                      <button className="btn btn-primary btn-sm" onClick={() => setViewingNote(n)}>View</button>
                      <button className="btn btn-ghost btn-sm"
                        onClick={() => { setEditingId(n.id); setEditTitle(n.title); setEditContent(n.content); }}>
                        Edit
                      </button>
                      <button className="btn btn-secondary btn-sm" onClick={() => handleDownloadNote(n.title, n.content)}>
                        Download
                      </button>
                      {deleteConfirmId === n.id ? (
                        <span className="del-confirm">
                          <span className="muted">Delete?</span>
                          <button className="btn btn-danger btn-sm" onClick={() => handleDeleteNote(n.id)}>Yes</button>
                          <button className="btn btn-ghost btn-sm" onClick={() => setDeleteConfirmId(null)}>No</button>
                        </span>
                      ) : (
                        <button className="btn btn-danger btn-sm" onClick={() => setDeleteConfirmId(n.id)}>Delete</button>
                      )}
                    </div>
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {viewingNote && (
        <div className="note-modal-overlay" onClick={() => setViewingNote(null)}>
          <div className="note-modal" onClick={(e) => e.stopPropagation()}>
            <div className="note-modal-header">
              <h4>{viewingNote.title}</h4>
              <button className="close-btn" onClick={() => setViewingNote(null)} aria-label="Close">×</button>
            </div>
            <div className="note-modal-body">
              <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                {viewingNote.content}
              </ReactMarkdown>
            </div>
            <div className="note-modal-footer">
              <button className="btn btn-secondary btn-sm"
                onClick={() => handleDownloadNote(viewingNote.title, viewingNote.content)}>
                Download .txt
              </button>
              <button className="btn btn-ghost btn-sm" onClick={() => setViewingNote(null)}>Close</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default Dashboard;
