import React, { useEffect, useState } from "react";
import { BrowserRouter, Routes, Route, Navigate, NavLink } from "react-router-dom";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import { ExamProvider, useExam } from "./contexts/ExamContext";
import { getLearningTree } from "./services/api";
import S from "./components/icons";
import Home from "./pages/Home";
import AuthPage from "./pages/AuthPage";
import Dashboard from "./pages/Dashboard";
import Profile from "./pages/Profile";
import Learn from "./pages/Learn";
import Papers from "./pages/Papers";
import Adaptive from "./pages/Adaptive";
import "./index.css";

function ProtectedRoute({ children }) {
  const { user } = useAuth();
  return user ? children : <Navigate to="/auth" />;
}

function initials(name) {
  return String(name || "S").trim().split(/\s+/).slice(0, 2)
    .map((w) => w[0]).join("").toUpperCase();
}

function ExamSwitcher() {
  const { currentExam, setCurrentExam } = useExam();
  const [exams, setExams] = useState([]);

  useEffect(() => {
    getLearningTree()
      .then((tree) => {
        const names = (tree || []).map((g) => g.exam).filter(Boolean);
        setExams(names);
      })
      .catch(() => {});
  }, []);

  if (exams.length === 0) return null;

  return (
    <div className="exam-switcher">
      <label className="exam-switcher-label">Exam</label>
      <select
        value={currentExam || ""}
        onChange={(e) => setCurrentExam(e.target.value)}
        className="exam-switcher-select"
      >
        <option value="">All exams</option>
        {exams.map((ex) => (
          <option key={ex} value={ex}>{ex}</option>
        ))}
      </select>
    </div>
  );
}

function ThemeToggle({ className }) {
  const [darkMode, setDarkMode] = useState(() => localStorage.getItem("sb-theme") === "dark");
  useEffect(() => {
    document.body.classList.toggle("dark-mode", darkMode);
    localStorage.setItem("sb-theme", darkMode ? "dark" : "light");
  }, [darkMode]);
  return (
    <button
      className={`theme-toggle ${className || ""}`}
      onClick={() => setDarkMode((v) => !v)}
      aria-label={darkMode ? "Switch to light mode" : "Switch to dark mode"}
      title={darkMode ? "Light mode" : "Dark mode"}
    >
      {darkMode ? <S.sun size={18} /> : <S.moon size={18} />}
    </button>
  );
}

function NavBar() {
  const { user, logout } = useAuth();
  if (!user) return null;
  return (
    <nav className="app-nav">
      <NavLink to="/" className="nav-brand" end title="StudyBuddy">
        <span className="nav-brand-mark"><S.book size={16} /></span>
        <span className="nav-brand-name">StudyBuddy</span>
      </NavLink>
      <NavLink to="/" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"} end>
        <S.chat size={16} /><span className="nav-link-label">Chat</span>
      </NavLink>
      <NavLink to="/dashboard" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        <S.dashboard size={16} /><span className="nav-link-label">Dashboard</span>
      </NavLink>
      <NavLink to="/learn" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        <S.book size={16} /><span className="nav-link-label">Learn</span>
      </NavLink>
      <NavLink to="/papers" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        <S.paper size={16} /><span className="nav-link-label">Papers</span>
      </NavLink>
      <NavLink to="/adaptive" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        <S.chart size={16} /><span className="nav-link-label">Adaptive</span>
      </NavLink>
      <NavLink to="/profile" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        <S.user size={16} /><span className="nav-link-label">Profile</span>
      </NavLink>
      <div className="nav-spacer" />
      <ExamSwitcher />
      <span className="nav-user">
        <span className="nav-avatar">{initials(user.username)}</span>
        {user.username}
      </span>
      <ThemeToggle className="nav-theme-toggle" />
      <button onClick={logout} className="logout-btn">
        <S.logout size={16} />Logout
      </button>
    </nav>
  );
}

function AppContent() {
  const { user } = useAuth();
  return (
    <>
      {!user && <ThemeToggle className="dark-toggle" />}
      <NavBar />
      <Routes>
        <Route path="/auth" element={user ? <Navigate to="/" /> : <AuthPage />} />
        <Route path="/" element={<ProtectedRoute><Home /></ProtectedRoute>} />
        <Route path="/dashboard" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
        <Route path="/learn" element={<ProtectedRoute><Learn /></ProtectedRoute>} />
        <Route path="/papers" element={<ProtectedRoute><Papers /></ProtectedRoute>} />
        <Route path="/adaptive" element={<ProtectedRoute><Adaptive /></ProtectedRoute>} />
        <Route path="/profile" element={<ProtectedRoute><Profile /></ProtectedRoute>} />
      </Routes>
    </>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ExamProvider>
          <AppContent />
        </ExamProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;