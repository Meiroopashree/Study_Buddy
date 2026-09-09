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

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    try {
      if (isLogin) {
        await login(email, password);
      } else {
        await signup(username, email, password);
      }
    } catch (err) {
      setError(err.message || "Authentication failed");
    }
  };

  return (
    <div className="auth-container">
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
          <button type="submit" className="auth-btn">{isLogin ? "Login" : "Sign Up"}</button>
        </form>
        <div className="auth-divider">or</div>
        <a className="google-btn" href={googleAuthUrl}><S.google size={18} />Continue with Google</a>
        <p className="auth-toggle" onClick={() => setIsLogin(!isLogin)}>
          {isLogin ? "Don't have an account? Sign up" : "Already have an account? Login"}
        </p>
      </div>
    </div>
  );
}

export default AuthPage;
