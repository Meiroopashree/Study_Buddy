import React, { useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { googleAuthUrl } from "../services/api";

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
        <a className="google-btn" href={googleAuthUrl}>Continue with Google</a>
        <p className="auth-toggle" onClick={() => setIsLogin(!isLogin)}>
          {isLogin ? "Don't have an account? Sign up" : "Already have an account? Login"}
        </p>
      </div>
    </div>
  );
}

export default AuthPage;
