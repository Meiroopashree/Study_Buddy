import React, { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import "katex/dist/katex.min.css";
import { useAuth } from "../contexts/AuthContext";
import { useExam } from "../contexts/ExamContext";
import { getDashboardStats, createNote, updateNote, deleteNote, getExamStats, getDueReviews } from "../services/api";
import S from "../components/icons";
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

function useCountUp(value, duration = 700, enabled = true) {
  const [display, setDisplay] = useState(value);
  const prev = useRef(value);
  useEffect(() => {
    if (!enabled) { setDisplay(value); prev.current = value; return; }
    const from = prev.current;
    const to = value;
    prev.current = value;
    if (from === to) { setDisplay(to); return; }
    const start = performance.now();
    let raf;
    const step = (t) => {
      const p = Math.min(1, (t - start) / duration);
      const eased = 1 - Math.pow(1 - p, 3);
      setDisplay(Math.round(from + (to - from) * eased));
      if (p < 1) raf = requestAnimationFrame(step);
    };
    raf = requestAnimationFrame(step);
    return () => cancelAnimationFrame(raf);
  }, [value, duration, enabled]);
  return enabled ? display : value;
}

function StatCard({ icon, value, label, hint, tone, animate }) {
  const shown = useCountUp(value, 700, animate);
  return (
    <div className="stat-card fade-rise" style={{ "--tone": tone }}>
      <span className="stat-icon">{icon}</span>
      <div className="stat-body">
        <span className="stat-value">{shown}</span>
        <span className="stat-label">{label}</span>
        {hint && <span className="stat-hint">{hint}</span>}
      </div>
    </div>
  );
}

function EmptyState({ icon, text, action, to }) {
  return (
    <div className="empty-state">
      <span className="empty-icon">{icon}</span>
      <p className="muted">{text}</p>
      {action && (
        <Link to={to} className="btn btn-ghost btn-sm">{action}</Link>
      )}
    </div>
  );
}

function Sk({ w = "100%", h = 14, r = 8, className = "", style }) {
  return (
    <div
      className={`sk ${className}`}
      style={{ width: w, height: h, borderRadius: r, ...style }}
    />
  );
}

function DashboardSkeleton() {
  const stats = [0, 1, 2, 3, 4, 5];
  return (
    <div className="dashboard-container" aria-busy="true" aria-label="Loading your dashboard">
      <div className="dash-hero">
        <div className="dash-hero-info">
          <Sk w="38%" h={30} r={10} />
          <Sk w="60%" h={14} r={8} style={{ marginTop: 12 }} />
        </div>
        <div className="hero-actions">
          <Sk w={96} h={38} r={10} />
          <Sk w={84} h={38} r={10} />
        </div>
      </div>

      <div className="stat-cards" style={{ marginTop: 0 }}>
        {stats.map((i) => (
          <div className="stat-card stat-card-sk" key={i} style={{ animationDelay: `${i * 40}ms` }}>
            <Sk w={40} h={40} r={12} className="stat-icon-sk" />
            <div className="stat-body">
              <Sk w={`${48 + (i % 4) * 10}%`} h={22} r={6} />
              <Sk w="82%" h={11} r={6} style={{ marginTop: 8 }} />
            </div>
          </div>
        ))}
      </div>

      <div className="dashboard-grid" style={{ marginTop: 0 }}>
        <div className="dashboard-panel" style={{ animationDelay: "120ms" }}>
          <Sk w="38%" h={18} r={6} />
          <div className="dash-list" style={{ marginTop: 16 }}>
            {[0, 1, 2].map((i) => (
              <div className="list-row-sk" key={i}>
                <div>
                  <Sk w={`${64 + i * 9}%`} h={13} r={6} />
                  <Sk w="40%" h={10} r={6} style={{ marginTop: 8 }} />
                </div>
                <div className="list-row-sk-right">
                  <Sk w={56} h={16} r={999} />
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="dashboard-panel" style={{ animationDelay: "180ms" }}>
          <Sk w="38%" h={18} r={6} />
          <div className="dash-list" style={{ marginTop: 16 }}>
            {[0, 1, 2].map((i) => (
              <div className="list-row-sk" key={i}>
                <div>
                  <Sk w={`${58 + i * 10}%`} h={13} r={6} />
                  <Sk w="36%" h={10} r={6} style={{ marginTop: 8 }} />
                </div>
                <div className="list-row-sk-right">
                  <Sk w={68} h={16} r={999} />
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
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
    return <DashboardSkeleton />;
  }

  const a = data?.analytics || {};
  const c = data?.counts || {};
  const uploads = data?.recentUploads || [];
  const quizzes = data?.recentQuizzes || [];
  const notes = data?.recentNotes || [];
  const userName = (user?.username || a?.profile?.username || "Student").split(" ")[0];

  const accuracy = a.accuracy ?? 0;
  const strength = accuracy >= 80 ? "outstanding" : accuracy >= 60 ? "solid" : accuracy > 0 ? "growing" : "";

  const tones = {
    indigo: "#4f46e5", violet: "#7c3aed", amber: "#f59e0b",
    emerald: "#10b981", sky: "#0ea5e9", rose: "#f43f5e",
  };

  return (
    <div className="dashboard-container">
      <header className="dash-hero fade-rise">
        <div>
          <span className="kicker">Your study overview</span>
          <h1 className="dashboard-title">{greeting()}, {userName}</h1>
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
          <Link to="/" className="btn btn-primary"><S.sparkle size={16} />Ask AI</Link>
          <Link to="/learn" className="btn btn-ghost"><S.book size={16} />Learn</Link>
        </div>
      </header>

      {error && <p className="auth-error dash-error">{error}</p>}

      <div className="stat-cards">
        <StatCard icon={<S.target size={20} />} value={accuracy} label="Quiz Accuracy" tone={tones.indigo}
          hint={accuracy > 0 ? `Best: ${a.bestScore ?? 0}` : null} animate />
        <StatCard icon={<S.clipboard size={20} />} value={a.quizzesTaken ?? 0} label="Quizzes Taken" tone={tones.violet}
          hint={a.avgScore != null ? `Avg score: ${a.avgScore}` : null} animate />
        <StatCard icon={<S.clock size={20} />} value={formatDuration(a.studyTimeTodaySec)} label="Study Time Today" tone={tones.emerald}
          hint={a.avgQuizTimeSec != null ? `Avg quiz: ${formatDuration(a.avgQuizTimeSec)}` : null} />
        <StatCard icon={<S.calendar size={20} />} value={formatDuration(a.totalStudyTimeSec)} label="Total Study Time" tone={tones.amber} />
        <StatCard icon={<S.notes size={20} />} value={c.notes ?? 0} label="Notes" tone={tones.sky} animate />
        <StatCard icon={<S.doc size={20} />} value={c.documents ?? 0} label="Documents" tone={tones.rose} animate />
      </div>

      {examStats.length > 0 && (
        <div className="dashboard-panel exam-stats-panel fade-rise">
          <div className="panel-header">
            <h3>Exam Progress</h3>
            <Link to="/learn" className="panel-more">Open Learn →</Link>
          </div>
          <div className="exam-stats-grid">
            {examStats.map((es, i) => (
              <div className="exam-stat-card fade-rise" key={es.exam} style={{ animationDelay: `${i * 60}ms` }}>
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
        <div className="dashboard-panel due-reviews-panel fade-rise">
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

      <div className="quick-actions fade-rise">
        <p className="quick-title">Quick Actions</p>
        <div className="quick-grid">
          <Link to="/learn" className="quick-card">
            <span className="quick-icon"><S.brain size={22} /></span>
            <span>Learn &amp; Practice</span>
            <span className="quick-hint">Syllabus, quizzes &amp; flashcards</span>
          </Link>
          <Link to="/papers" className="quick-card">
            <span className="quick-icon"><S.paper size={22} /></span>
            <span>Question Papers</span>
            <span className="quick-hint">Upload &amp; practice real papers</span>
          </Link>
          <Link to="/adaptive" className="quick-card">
            <span className="quick-icon"><S.trending size={22} /></span>
            <span>Adaptive Learning</span>
            <span className="quick-hint">Personalized daily plan</span>
          </Link>
          <Link to="/profile" className="quick-card">
            <span className="quick-icon"><S.user size={22} /></span>
            <span>Profile</span>
            <span className="quick-hint">View your stats</span>
          </Link>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="dashboard-panel fade-rise">
          <div className="panel-header">
            <h3>Recent Quizzes</h3>
            {quizzes.length > 0 && <Link to="/learn" className="panel-more">View all →</Link>}
          </div>
          {quizzes.length === 0 ? (
            <EmptyState icon={<S.clipboard size={28} />} text="No quizzes yet." action="Take a quiz" to="/learn" />
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

        <div className="dashboard-panel fade-rise">
          <div className="panel-header">
            <h3>Recent Uploads</h3>
            {uploads.length > 0 && <Link to="/papers" className="panel-more">View all →</Link>}
          </div>
          {uploads.length === 0 ? (
            <EmptyState icon={<S.paper size={28} />} text="No uploads yet." action="Upload a paper" to="/papers" />
          ) : (
            <ul className="dash-list">
              {uploads.map((u) => (
                <li key={u.id} className="doc-row">
                  <span className="doc-icon"><S.doc size={18} /></span>
                  <span className="doc-title">{u.title}</span>
                  <span className="muted">{new Date(u.uploadedAt).toLocaleDateString()}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="dashboard-panel notes-panel fade-rise">
        <div className="panel-header">
          <h3><S.notes size={18} /> My Notes</h3>
          {notes.length > 0 && <Link to="/learn" className="panel-more">All notes →</Link>}
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
          <EmptyState icon={<S.notes size={28} />} text="No notes yet. Capture your first idea above!" />
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
                        <S.edit size={14} />Edit
                      </button>
                      <button className="btn btn-secondary btn-sm" onClick={() => handleDownloadNote(n.title, n.content)}>
                        <S.download size={14} />Download
                      </button>
                      {deleteConfirmId === n.id ? (
                        <span className="del-confirm">
                          <span className="muted">Delete?</span>
                          <button className="btn btn-danger btn-sm" onClick={() => handleDeleteNote(n.id)}>Yes</button>
                          <button className="btn btn-ghost btn-sm" onClick={() => setDeleteConfirmId(null)}>No</button>
                        </span>
                      ) : (
                        <button className="btn btn-danger btn-sm" onClick={() => setDeleteConfirmId(n.id)}>
                          <S.trash size={14} />Delete
                        </button>
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
              <button className="close-btn" onClick={() => setViewingNote(null)} aria-label="Close">
                <S.x size={18} />
              </button>
            </div>
            <div className="note-modal-body">
              <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                {viewingNote.content}
              </ReactMarkdown>
            </div>
            <div className="note-modal-footer">
              <button className="btn btn-secondary btn-sm"
                onClick={() => handleDownloadNote(viewingNote.title, viewingNote.content)}>
                <S.download size={14} />Download .txt
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