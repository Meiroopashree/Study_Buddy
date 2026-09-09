import React, { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { getProfile, updateProfile } from "../services/api";
import S from "../components/icons";
import "../styles/Dashboard.css";

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

function Profile() {
  const { user, updateUser } = useAuth();
  const [profile, setProfile] = useState(null);
  const [username, setUsername] = useState("");
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!user) return;
    getProfile(user.token).then((p) => {
      setProfile(p);
      setUsername(p.username);
    }).catch(() => setError("Failed to load profile."));
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
  const stats = profile?.stats;

  return (
    <div className="dashboard-container">
      <h1 className="dashboard-title">Profile</h1>
      {error && <p className="auth-error">{error}</p>}
      {saved && <p className="save-ok">Profile updated.</p>}

      <div className="profile-card">
        <div className="profile-avatar">{initials(profile?.username || user.username)}</div>
        <div className="profile-info">
          <h3>{profile?.username || user.username}</h3>
          <p className="muted">{profile?.email || user.email}</p>
          <p className="muted">
            Account: {(profile?.provider || user.provider || "email")}
            {" · "}Joined: {profile?.createdAt ? new Date(profile.createdAt).toLocaleDateString() : "-"}
          </p>
        </div>
      </div>

      <div className="stat-cards">
        <div className="stat-card" style={{ "--tone": "#4f46e5" }}>
          <span className="stat-icon"><S.target size={18} /></span>
          <div className="stat-body"><span className="stat-value">{stats?.accuracy ?? 0}%</span><span className="stat-label">Accuracy</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#10b981" }}>
          <span className="stat-icon"><S.clipboard size={18} /></span>
          <div className="stat-body"><span className="stat-value">{stats?.quizzesTaken ?? 0}</span><span className="stat-label">Quizzes Taken</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#0ea5e9" }}>
          <span className="stat-icon"><S.doc size={18} /></span>
          <div className="stat-body"><span className="stat-value">{stats?.documents ?? 0}</span><span className="stat-label">Documents</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#f59e0b" }}>
          <span className="stat-icon"><S.book size={18} /></span>
          <div className="stat-body"><span className="stat-value">{stats?.notes ?? 0}</span><span className="stat-label">Notes</span></div>
        </div>
        <div className="stat-card" style={{ "--tone": "#7c3aed" }}>
          <span className="stat-icon"><S.clock size={18} /></span>
          <div className="stat-body"><span className="stat-value">{formatDuration(stats?.totalQuizTimeSec)}</span><span className="stat-label">Quiz Time</span></div>
        </div>
      </div>

      <div className="dashboard-panel" style={{ maxWidth: 480 }}>
        <h3>Edit Profile</h3>
        <form className="note-form" onSubmit={handleSave}>
          <input type="text" placeholder="Username" value={username}
            onChange={(e) => setUsername(e.target.value)} />
          <button type="submit" className="btn btn-primary">Save</button>
        </form>
      </div>
    </div>
  );
}

export default Profile;
