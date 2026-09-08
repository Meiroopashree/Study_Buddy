import React, { createContext, useContext, useState } from "react";
import { login as apiLogin, signup as apiSignup, getProfile } from "../services/api";

const AuthContext = createContext(null);

function getInitialUser() {
  const params = new URLSearchParams(window.location.search);
  const token = params.get("token");
  if (token) {
    const user = {
      token,
      username: params.get("username") || "Student",
      email: params.get("email") || "",
      provider: "google"
    };
    window.history.replaceState({}, document.title, window.location.pathname);
    return user;
  }
  const stored = localStorage.getItem("user");
  return stored ? JSON.parse(stored) : null;
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(getInitialUser);

  const login = async (email, password) => {
    const data = await apiLogin(email, password);
    const profile = await getProfile(data.token);
    const userData = { ...data, stats: profile.stats };
    setUser(userData);
    localStorage.setItem("user", JSON.stringify(userData));
    return userData;
  };

  const signup = async (username, email, password) => {
    const data = await apiSignup(username, email, password);
    const profile = await getProfile(data.token);
    const userData = { ...data, stats: profile.stats };
    setUser(userData);
    localStorage.setItem("user", JSON.stringify(userData));
    return userData;
  };

  const updateUser = (data) => {
    const next = { ...user, ...data };
    setUser(next);
    localStorage.setItem("user", JSON.stringify(next));
  };

  const logout = () => {
    setUser(null);
    localStorage.removeItem("user");
  };

  return (
    <AuthContext.Provider value={{ user, login, signup, updateUser, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
