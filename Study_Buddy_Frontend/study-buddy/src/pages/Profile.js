import React, { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { useExam } from "../contexts/ExamContext";
import { getProfile, updateProfile, getQuizHistory, getLearningTree } from "../services/api";
import S from "../components/icons";
import useCountUp from "../hooks/useCountUp";
import "../styles/Dashboard.css";
import "../styles/Profile.css";

function formatDuration(sec) {
  sec = Math.max(0, Math.round(sec || 0));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m`;
  return `${sec}s`;
}

function initials(name) {
  return (name || "?").trim().split(/\s+/).slice(0, 2).map((p) => p[0]?.toUpperCase() || "").join("");
}

function dayKey(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function computeStreak(history) {
  if (!history.length) return 0;
  const days = new Set(history.map((h) => dayKey(new Date(h.completedAt))));
  const cursor = new Date();
  if (!days.has(dayKey(cursor))) {
    cursor.setDate(cursor.getDate() - 1);
    if (!days.has(dayKey(cursor))) return 0;
  }
  let streak = 0;
  while (days.has(dayKey(cursor))) {
    streak += 1;
    cursor.setDate(cursor.getDate() - 1);
  }
  return streak;
}

function accuracyOfQuiz(h) {
  return h && h.totalQuestions ? Math.round((h.score / h.totalQuestions) * 100) : 0;
}

function timeAgo(date) {
  const mins = Math.floor((Date.now() - date.getTime()) / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  if (days < 7) return `${days}d ago`;
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

const DIFF_TONES = {
  easy: "var(--success)",
  medium: "var(--accent)",
  hard: "var(--danger)",
  expert: "var(--info)",
};

const SPARKLES = [
  { left: "12%", top: "20%", delay: "0s", size: 11 },
  { left: "84%", top: "18%", delay: "1.3s", size: 14 },
  { left: "72%", top: "76%", delay: "0.5s", size: 9 },
  { left: "22%", top: "72%", delay: "2s", size: 12 },
  { left: "48%", top: "8%", delay: "0.9s", size: 8 },
];

function tiltIn(e) {
  const el = e.currentTarget;
  const r = el.getBoundingClientRect();
  const px = (e.clientX - r.left) / r.width - 0.5;
  const py = (e.clientY - r.top) / r.height - 0.5;
  el.style.setProperty("--rx", (py * -5).toFixed(2) + "deg");
  el.style.setProperty("--ry", (px * 5).toFixed(2) + "deg");
}

function tiltOut(e) {
  e.currentTarget.style.setProperty("--rx", "0deg");
  e.currentTarget.style.setProperty("--ry", "0deg");
}

function Profile() {
  const { user, updateUser } = useAuth();
  const { currentExam, setCurrentExam } = useExam();
  const [profile, setProfile] = useState(null);
  const [username, setUsername] = useState("");
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");
  const [history, setHistory] = useState(null);
  const [exams, setExams] = useState([]);

  const stats = profile?.stats;
  const accuracy = stats?.accuracy ?? 0;
  const quizzesTaken = stats?.quizzesTaken ?? 0;
  const documents = stats?.documents ?? 0;
  const notes = stats?.notes ?? 0;
  const totalTimeSec = stats?.totalQuizTimeSec ?? 0;

  const hist = history || [];
  const avgAccuracy = hist.length ? hist.reduce((s, h) => s + accuracyOfQuiz(h), 0) / hist.length : 0;
  const bestAccuracy = hist.reduce((m, h) => Math.max(m, accuracyOfQuiz(h)), 0);
  const streak = computeStreak(hist);
  const todayCount = hist.filter((h) => dayKey(new Date(h.completedAt)) === dayKey(new Date())).length;
  const questionsAnswered = hist.reduce((s, h) => s + (h.totalQuestions || 0), 0);

  const acc = useCountUp(accuracy, { decimals: 1, duration: 1000 });
  const quizzesN = useCountUp(quizzesTaken);
  const docsN = useCountUp(documents);
  const notesN = useCountUp(notes);
  const qaN = useCountUp(questionsAnswered);
  const avgN = useCountUp(avgAccuracy, { decimals: 1, duration: 1000 });
  const bestN = useCountUp(bestAccuracy, { decimals: 1, duration: 1000 });
  const streakN = useCountUp(streak, { duration: 700 });
  const todayN = useCountUp(todayCount, { duration: 700 });

  useEffect(() => {
    if (!user) return;
    let live = true;
    getProfile(user.token)
      .then((p) => {
        if (!live) return;
        setProfile(p);
        setUsername(p.username || user.username || "");
      })
      .catch(() => live && setError("Failed to load profile."));
    getQuizHistory()
      .then((h) => live && setHistory(Array.isArray(h) ? h : []))
      .catch(() => live && setHistory([]));
    getLearningTree()
      .then((t) => live && setExams((t || []).map((g) => g.exam).filter(Boolean)))
      .catch(() => {});
    return () => { live = false; };
  }, [user]);

  const handleSave = async (e) => {
    e.preventDefault();
    setError("");
    setSaved(false);
    try {
      const updated = await updateProfile(username);
      updateUser({ username: updated.username });
      setProfile({ ...profile, username: updated.username });
      setSaved(true);
    } catch (err) {
      setError(err.message);
    }
  };

  if (!user) return null;

  const achievements = [
    { key: "first", icon: S.target, label: "First Steps", desc: "Complete your first quiz", done: quizzesTaken >= 1, tone: "#4f46e5" },
    { key: "century", icon: S.star, label: "Century Club", desc: "Reach 90%+ overall accuracy", done: accuracy >= 90, tone: "#f59e0b" },
    { key: "streak", icon: S.trending, label: "On a Roll", desc: "Keep a 3-day study streak", done: streak >= 3, tone: "#10b981" },
    { key: "machine", icon: S.clipboard, label: "Quiz Machine", desc: "Complete 15 quizzes", done: quizzesTaken >= 15, tone: "#8b5cf6" },
    { key: "notes", icon: S.notebook, label: "Note Taker", desc: "Save 5 notes", done: notes >= 5, tone: "#0ea5e9" },
    { key: "docs", icon: S.doc, label: "Library Card", desc: "Upload 10 documents", done: documents >= 10, tone: "#ec4899" },
    { key: "marathon", icon: S.clock, label: "Marathoner", desc: "Log 2+ hours of quiz time", done: totalTimeSec >= 7200, tone: "#f43f5e" },
    { key: "perfect", icon: S.alert, label: "Perfectionist", desc: "Score 100% on a quiz", done: hist.some((h) => h.totalQuestions > 0 && h.score === h.totalQuestions), tone: "#14b8a6" },
  ];

  const chart = hist.slice(0, 8).reverse();

  return (
    <div className="dashboard-container profile-page">
      {error && <p className="auth-error dash-error">{error}</p>}
      {saved && <p className="save-ok">Profile updated.</p>}

      <section className="pf-hero tilt-card" onMouseMove={tiltIn} onMouseLeave={tiltOut}>
        <div className="pf-aurora" />
        {SPARKLES.map((s, i) => (
          <span key={i} className="pf-sparkle" style={{ left: s.left, top: s.top, width: s.size, height: s.size, animationDelay: s.delay }} />
        ))}
        <div className="pf-avatar-wrap">
          <span className="pf-avatar-ring" />
          <div className="pf-avatar">{initials(profile?.username || user.username)}</div>
          <span className="pf-avatar-chip"><S.sparkle size={11} /></span>
        </div>
        <div className="pf-hero-info">
          <span className="kicker">Student Profile</span>
          <h1 className="pf-name">{profile?.username || user.username}</h1>
          <p className="pf-email">{profile?.email || user.email || "student@studybuddy"}</p>
          <div className="pf-badges">
            <span className="badge badge--primary"><S.user size={13} /> {(profile?.provider || user.provider || "email").replace(/^./, (c) => c.toUpperCase())}</span>
            <span className="badge"><S.calendar size={13} /> Joined {profile?.createdAt ? new Date(profile.createdAt).toLocaleDateString(undefined, { month: "short", year: "numeric" }) : "—"}</span>
            {currentExam && <span className="badge badge--accent"><S.target size={13} /> {currentExam}</span>}
          </div>
        </div>
        <div className="pf-hero-mini">
          <div className="pf-mini"><strong>{streakN}</strong><span>day streak</span></div>
          <div className="pf-mini"><strong>{todayN}</strong><span>today</span></div>
          <div className="pf-mini"><strong>{bestN}%</strong><span>best score</span></div>
        </div>
      </section>

      <h2 className="pf-section-title">Overview</h2>
      <div className="stat-cards">
        <div className="stat-card" style={{ "--tone": "#4f46e5" }}>
          <span className="stat-icon"><S.target size={18} /></span>
          <div className="stat-body"><span className="stat-value">{acc.toFixed(1)}%</span><span className="stat-label">Accuracy</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#10b981" }}>
          <span className="stat-icon"><S.clipboard size={18} /></span>
          <div className="stat-body"><span className="stat-value">{quizzesN}</span><span className="stat-label">Quizzes Taken</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#0ea5e9" }}>
          <span className="stat-icon"><S.doc size={18} /></span>
          <div className="stat-body"><span className="stat-value">{docsN}</span><span className="stat-label">Documents</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#f59e0b" }}>
          <span className="stat-icon"><S.book size={18} /></span>
          <div className="stat-body"><span className="stat-value">{notesN}</span><span className="stat-label">Notes</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#7c3aed" }}>
          <span className="stat-icon"><S.clock size={18} /></span>
          <div className="stat-body"><span className="stat-value">{formatDuration(totalTimeSec)}</span><span className="stat-label">Quiz Time</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#f43f5e" }}>
          <span className="stat-icon"><S.brain size={18} /></span>
          <div className="stat-body"><span className="stat-value">{qaN}</span><span className="stat-label">Questions Answered</span></div>
        </div>
      </div>

      <div className="pf-grid">
        <section className="dashboard-panel pf-panel">
          <div className="panel-header">
            <h3><S.chart size={17} /> Performance</h3>
          </div>
          <div className="pf-perf-stats">
            <div className="pf-perf"><span>Average</span><strong>{avgN.toFixed(1)}%</strong></div>
            <div className="pf-perf"><span>Best</span><strong>{bestN.toFixed(1)}%</strong></div>
            <div className="pf-perf"><span>Streak</span><strong>{streakN} days</strong></div>
            <div className="pf-perf"><span>Today</span><strong>{todayN} quiz</strong></div>
          </div>
          {chart.length ? (
            <div className="pf-chart">
              {chart.map((h, i) => {
                const pct = accuracyOfQuiz(h);
                return (
                  <div className="pf-bar-col" key={h.id ?? i} title={`${h.topic} — ${pct}%`}>
                    <span className="pf-bar-label">{pct}%</span>
                    <div className="pf-bar"><span className="pf-bar-fill" style={{ "--h": `${pct}%`, "--i": i }} /></div>
                    <span className="pf-bar-day">{new Date(h.completedAt).getDate()}</span>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="pf-empty">Complete a few quizzes to see your accuracy trend here.</p>
          )}
        </section>

        <section className="dashboard-panel pf-panel">
          <div className="panel-header">
            <h3><S.sparkle size={17} /> Achievements</h3>
          </div>
          <div className="pf-ach-grid">
            {achievements.map((a, i) => (
              <div key={a.key} className={`pf-ach ${a.done ? "done" : "locked"}`} style={{ "--tone": a.tone, "--i": i }}>
                <span className="pf-ach-icon"><a.icon size={17} /></span>
                <span className="pf-ach-label">{a.label}</span>
                <span className="pf-ach-desc">{a.desc}</span>
                {a.done && <span className="pf-ach-check"><S.check size={13} /></span>}
              </div>
            ))}
          </div>
        </section>
      </div>

      <section className="dashboard-panel pf-panel">
        <div className="panel-header">
          <h3><S.clock size={17} /> Recent Activity</h3>
        </div>
        {history === null ? (
          <div className="pf-activity">
            {[0, 1, 2, 3].map((i) => (
              <div className="pf-act-row" key={i}>
                <span className="pf-skel shimmer" style={{ width: 38, height: 38, borderRadius: 12 }} />
                <span className="pf-act-body">
                  <span className="pf-skel shimmer" style={{ width: "55%", height: 14 }} />
                  <span className="pf-skel shimmer" style={{ width: "34%", height: 11, marginTop: 8 }} />
                </span>
              </div>
            ))}
          </div>
        ) : hist.length === 0 ? (
          <p className="pf-empty">No quizzes yet — head to Learn or Chat to take your first one.</p>
        ) : (
          <div className="pf-activity">
            {hist.slice(0, 6).map((h) => {
              const pct = accuracyOfQuiz(h);
              const tone = DIFF_TONES[(h.difficulty || "medium").toLowerCase()] || "var(--primary)";
              return (
                <div className="pf-act-row" key={h.id}>
                  <span className="pf-act-icon" style={{ color: tone, background: `color-mix(in srgb, ${tone} 13%, transparent)`, borderColor: `color-mix(in srgb, ${tone} 24%, transparent)` }}>
                    <S.clipboard size={16} />
                  </span>
                  <span className="pf-act-body">
                    <span className="pf-act-title">{h.topic || "Quiz"}</span>
                    <span className="pf-act-meta">{h.score}/{h.totalQuestions} · {(h.difficulty || "medium")}</span>
                  </span>
                  <span className="pf-act-right">
                    <span className={`badge ${pct >= 80 ? "badge--success" : pct >= 50 ? "badge--accent" : "badge--danger"}`}>{pct}%</span>
                    <span className="pf-act-time">{timeAgo(new Date(h.completedAt))}</span>
                  </span>
                </div>
              );
            })}
          </div>
        )}
      </section>

      <section className="dashboard-panel pf-panel" style={{ maxWidth: 520 }}>
        <div className="panel-header">
          <h3><S.edit size={17} /> Settings</h3>
        </div>
        <form className="pf-form" onSubmit={handleSave}>
          {exams.length > 0 && (
            <label className="pf-field">
              <span className="pf-field-label">Focus exam</span>
              <select value={currentExam || ""} onChange={(e) => setCurrentExam(e.target.value)}>
                <option value="">All exams</option>
                {exams.map((ex) => (
                  <option key={ex} value={ex}>{ex}</option>
                ))}
              </select>
            </label>
          )}
          <label className="pf-field">
            <span className="pf-field-label">Username</span>
            <input type="text" placeholder="Username" value={username}
              onChange={(e) => setUsername(e.target.value)} />
          </label>
          <div className="pf-actions">
            <button type="submit" className="btn btn-primary">Save changes</button>
          </div>
        </form>
      </section>
    </div>
  );
}

export default Profile;