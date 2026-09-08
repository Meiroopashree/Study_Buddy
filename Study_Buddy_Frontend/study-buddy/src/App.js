import React, { useEffect, useState } from "react";
import { BrowserRouter, Routes, Route, Navigate, NavLink } from "react-router-dom";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import { ExamProvider, useExam } from "./contexts/ExamContext";
import { getLearningTree } from "./services/api";
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
      <label className="exam-switcher-label">Exam:</label>
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

function NavBar() {
  const { user, logout } = useAuth();
  if (!user) return null;
  return (
    <nav className="app-nav">
      <NavLink to="/" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"} end>
        Chat
      </NavLink>
      <NavLink to="/dashboard" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        Dashboard
      </NavLink>
      <NavLink to="/learn" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        Learn
      </NavLink>
      <NavLink to="/papers" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        Question Papers
      </NavLink>
      <NavLink to="/adaptive" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        Adaptive
      </NavLink>
      <NavLink to="/profile" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
        Profile
      </NavLink>
      <div className="nav-spacer" />
      <ExamSwitcher />
      <span className="nav-user">Hi, {user.username}</span>
      <button onClick={logout} className="logout-btn">Logout</button>
    </nav>
  );
}

function AppContent() {
  const [darkMode, setDarkMode] = useState(() => localStorage.getItem("sb-theme") === "dark");
  const { user } = useAuth();

  useEffect(() => {
    document.body.classList.toggle("dark-mode", darkMode);
    localStorage.setItem("sb-theme", darkMode ? "dark" : "light");
  }, [darkMode]);

  return (
    <>
      <button
        className="dark-toggle"
        onClick={() => setDarkMode((v) => !v)}
        aria-label="Toggle dark mode"
      >
        {darkMode ? "☀️" : "🌙"}
      </button>
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
