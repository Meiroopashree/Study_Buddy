import React, { useEffect, useState } from "react";
import { BrowserRouter, Routes, Route, Navigate, NavLink, useLocation } from "react-router-dom";
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
      <label className="exam-switcher-label" htmlFor="exam-switcher">Exam</label>
      <select
        id="exam-switcher"
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

const NAV_SECTIONS = [
  {
    title: "Study",
    items: [
      { to: "/", end: true, icon: S.chat, label: "Chat" },
      { to: "/dashboard", icon: S.dashboard, label: "Dashboard" },
      { to: "/learn", icon: S.book, label: "Learn" },
      { to: "/papers", icon: S.paper, label: "Papers" },
      { to: "/adaptive", icon: S.chart, label: "Adaptive" },
    ],
  },
  {
    title: "Account",
    items: [
      { to: "/profile", icon: S.user, label: "Profile" },
    ],
  },
];

function SidebarNav({ onNavigate }) {
  return (
    <>
      <NavLink to="/" end className="sidebar-brand" onClick={onNavigate} title="StudyBuddy">
        <span className="sidebar-brand-mark"><S.book size={20} /></span>
        <span className="sidebar-brand-name">StudyBuddy</span>
      </NavLink>
      {NAV_SECTIONS.map((section) => (
        <div className="sidebar-section" key={section.title}>
          <span className="sidebar-section-title">{section.title}</span>
          <nav className="sidebar-nav">
            {section.items.map((item) => (
              <NavLink
                key={item.label}
                to={item.to}
                end={item.end}
                onClick={onNavigate}
                className={({ isActive }) => isActive ? "sidebar-link active" : "sidebar-link"}
              >
                <item.icon size={18} />
                <span>{item.label}</span>
              </NavLink>
            ))}
          </nav>
        </div>
      ))}
    </>
  );
}

function SidebarFooter() {
  const { user, logout } = useAuth();
  return (
    <div className="sidebar-footer">
      <ExamSwitcher />
      {user && (
        <div className="user-chip">
          <span className="user-chip-avatar">{initials(user.username)}</span>
          <span className="user-chip-info">
            <span className="user-chip-name">{user.username}</span>
            <span className="user-chip-role">StudyBuddy member</span>
          </span>
        </div>
      )}
      <div className="sidebar-tools">
        <ThemeToggle />
        <button onClick={logout} className="logout-btn">
          <S.logout size={16} />Logout
        </button>
      </div>
    </div>
  );
}

function SidebarContent({ onNavigate }) {
  return (
    <>
      <SidebarNav onNavigate={onNavigate} />
      <SidebarFooter />
    </>
  );
}

function MobileDrawer({ open, onClose }) {
  return (
    <>
      <div className={`drawer-scrim ${open ? "open" : ""}`} onClick={onClose} aria-hidden="true" />
      <div className={`drawer ${open ? "open" : ""}`} role="dialog" aria-modal="true">
        <div className="drawer-inner">
          <button className="close-btn drawer-close" onClick={onClose} aria-label="Close menu">
            <S.x size={18} />
          </button>
          <SidebarContent onNavigate={onClose} />
        </div>
      </div>
    </>
  );
}

function TopBar({ onOpenDrawer }) {
  return (
    <header className="app-topbar">
      <NavLink to="/" end className="topbar-brand" title="StudyBuddy">
        <span className="topbar-brand-mark"><S.book size={16} /></span>
        <span className="topbar-brand-name">StudyBuddy</span>
      </NavLink>
      <div className="topbar-spacer" />
      <div className="topbar-tools">
        <ThemeToggle />
        <button
          className="sidebar-toggle"
          onClick={onOpenDrawer}
          aria-label="Open menu"
          title="Menu"
        >
          <S.menu size={20} />
        </button>
      </div>
    </header>
  );
}

function AppContent() {
  const { user } = useAuth();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    setDrawerOpen(false);
  }, [location]);

  useEffect(() => {
    if (!drawerOpen) return;
    const onKey = (e) => {
      if (e.key === "Escape") setDrawerOpen(false);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [drawerOpen]);

  if (!user) {
    return (
      <>
        <ThemeToggle className="dark-toggle" />
        <Routes>
          <Route path="/auth" element={<AuthPage />} />
          <Route path="*" element={<Navigate to="/auth" />} />
        </Routes>
      </>
    );
  }

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <div className="sidebar-inner">
          <SidebarContent />
        </div>
      </aside>

      <MobileDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />

      <div className="app-main">
        <TopBar onOpenDrawer={() => setDrawerOpen(true)} />
        <div className="page-enter" key={location.pathname}>
          <Routes>
            <Route path="/auth" element={<Navigate to="/" />} />
            <Route path="/" element={<Home />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/learn" element={<Learn />} />
            <Route path="/papers" element={<Papers />} />
            <Route path="/adaptive" element={<Adaptive />} />
            <Route path="/profile" element={<Profile />} />
            <Route path="*" element={<Navigate to="/" />} />
          </Routes>
        </div>
      </div>
    </div>
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