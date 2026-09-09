import React, { useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { googleAuthUrl } from "../services/api";
import S from "../components/icons";

function AuthPage() {
  const { login, signup } = useAuth();
  const [isLogin, setIsLogin] = useState(true);
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      if (isLogin) {
        await login(email, password);
      } else {
        await signup(username, email, password);
      }
    } catch (err) {
      setError(err.message || "Authentication failed");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-hero">
        <div className="auth-hero-brand">
          <span className="auth-hero-mark"><S.book size={22} /></span>
          <span className="auth-hero-name">StudyBuddy</span>
        </div>
        <div className="auth-hero-body">
          <h1 className="auth-hero-title">
            Your AI study partner for <em>every exam</em>
          </h1>
          <p className="auth-hero-text">
            Learn any topic in depth, take adaptive quizzes, understand concepts
            with flashcards, and stay ahead with a revision schedule built around
            your syllabus.
          </p>
          <ul className="auth-hero-features">
            <li>
              <span className="auth-hero-feature-icon"><S.sparkle size={16} /></span>
              AI-generated lessons, notes, formulas &amp; concept maps
            </li>
            <li>
              <span className="auth-hero-feature-icon"><S.clipboard size={16} /></span>
              Smart quizzes with instant scoring and explanations
            </li>
            <li>
              <span className="auth-hero-feature-icon"><S.brain size={16} /></span>
              Flashcards and spaced-revision scheduling
            </li>
          </ul>
        </div>
        <div className="auth-hero-foot">
          <div className="dots">
            <span className="dot" />
            <span className="dot" />
            <span className="dot" />
          </div>
          StudyBuddy · Learn deeper, remember longer
        </div>
      </div>

      <div className="auth-panel">
        <div className="auth-card">
        <div className="auth-brand">
          <span className="auth-brand-mark"><S.book size={24} /></span>
          <span className="auth-brand-name">StudyBuddy</span>
        </div>
        <h2>{isLogin ? "Login" : "Sign Up"}</h2>
        <form onSubmit={handleSubmit}>
          {!isLogin && (
            <input type="text" placeholder="Username" value={username}
              onChange={(e) => setUsername(e.target.value)} className="auth-input" required />
          )}
          <input type="email" placeholder="Email" value={email}
            onChange={(e) => setEmail(e.target.value)} className="auth-input" required />
          <input type="password" placeholder="Password" value={password}
            onChange={(e) => setPassword(e.target.value)} className="auth-input" required />
          {error && <p className="auth-error">{error}</p>}
          <button type="submit" className="auth-btn" disabled={submitting}>
            {submitting ? "Please wait…" : (isLogin ? "Login" : "Sign Up")}
          </button>
        </form>
        <div className="auth-divider">or</div>
        <a className="google-btn" href={googleAuthUrl}><S.google size={18} />Continue with Google</a>
        <p className="auth-toggle" onClick={() => setIsLogin(!isLogin)}>
          {isLogin ? "Don't have an account? Sign up" : "Already have an account? Login"}
        </p>
        </div>
      </div>
    </div>
  );
}

export default AuthPage;
