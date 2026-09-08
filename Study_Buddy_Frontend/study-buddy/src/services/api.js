const API_BASE = process.env.REACT_APP_API_BASE || "http://localhost:5193/api";
const STUDY_URL = `${API_BASE}/study`;
const AUTH_URL = `${API_BASE}/auth`;

function getAuthHeaders() {
  try {
    const stored = localStorage.getItem("user");
    if (!stored) return {};
    const user = JSON.parse(stored);
    return user.token ? { authorization: user.token } : {};
  } catch {
    return {};
  }
}

export const askAI = async (question, history = [], documentIds = [], imageUrl = null, mode = "NCERT") => {
  const res = await fetch(`${STUDY_URL}/ask`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question, history, documentIds, imageUrl, mode }),
  });
  if (!res.ok) throw new Error((await res.json()).message || "Request failed");
  return res.json();
};

export const generateQuiz = async (topic, difficulty = "medium", questionCount = 10, exam = "JEE Advanced") => {
  const res = await fetch(`${STUDY_URL}/quiz`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ topic, difficulty, questionCount, exam }),
  });
  if (!res.ok) throw new Error((await res.json()).message || "Request failed");
  return res.json();
};

export const saveQuizResult = async (result) => {
  const res = await fetch(`${STUDY_URL}/quiz/save`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify(result),
  });
  return res.json();
};

export const getQuizHistory = async () => {
  const res = await fetch(`${STUDY_URL}/quiz/history`, { headers: getAuthHeaders() });
  return res.json();
};

export const getQuizDetail = async (id) => {
  const res = await fetch(`${STUDY_URL}/quiz/history/${id}`, { headers: getAuthHeaders() });
  return res.json();
};

export const signup = async (username, email, password) => {
  const res = await fetch(`${AUTH_URL}/signup`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, email, password }),
  });
  if (!res.ok) {
    const msg = (await res.text()) || "Signup failed";
    throw new Error(msg);
  }
  return res.json();
};

export const login = async (email, password) => {
  const res = await fetch(`${AUTH_URL}/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    const msg = (await res.text()) || "Login failed";
    throw new Error(msg);
  }
  return res.json();
};

export const getProfile = async (token) => {
  const res = await fetch(`${AUTH_URL}/profile`, {
    headers: { authorization: token },
  });
  return res.json();
};

export const askAIStream = async (question, history, onChunk, onDone, documentIds = [], imageUrl = null, mode = "NCERT") => {
  const res = await fetch(`${STUDY_URL}/ask/stream`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question, history, documentIds, imageUrl, mode }),
  });
  if (!res.ok) throw new Error("Stream request failed");

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    const lines = buffer.split("\n");
    buffer = lines.pop() || "";
    for (const line of lines) {
      if (!line.startsWith("data: ")) continue;
      const json = line.slice(6);
      try {
        const data = JSON.parse(json);
        if (data.done) { onDone?.(); return; }
        if (data.chunk) onChunk?.(data.chunk);
      } catch { /* skip malformed */ }
    }
  }
  onDone?.();
};

const DOC_URL = `${API_BASE}/documents`;

export const getDocuments = async () => {
  const res = await fetch(DOC_URL, { headers: getAuthHeaders() });
  return res.json();
};

export const uploadDocument = async (file) => {
  const formData = new FormData();
  formData.append("file", file);
  const res = await fetch(`${DOC_URL}/upload`, {
    method: "POST",
    body: formData,
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Upload failed";
    try { msg = JSON.parse(body).error || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const uploadDocumentText = async (title, content) => {
  const res = await fetch(`${DOC_URL}/upload-text`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ title, content }),
  });
  return res.json();
};

export const deleteDocument = async (id) => {
  await fetch(`${DOC_URL}/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
};

export const uploadImage = async (file) => {
  const formData = new FormData();
  formData.append("file", file);
  const res = await fetch(`${API_BASE}/upload/image`, {
    method: "POST",
    body: formData,
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Upload failed";
    try { msg = JSON.parse(body).error || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

const NOTES_URL = `${API_BASE}/notes`;
const DASHBOARD_URL = `${API_BASE}/dashboard`;

export const getNotes = async () => {
  const res = await fetch(NOTES_URL, { headers: getAuthHeaders() });
  return res.json();
};

export const createNote = async (title, content) => {
  const res = await fetch(NOTES_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ title, content }),
  });
  if (!res.ok) throw new Error("Failed to create note");
  return res.json();
};

export const updateNote = async (id, title, content) => {
  const res = await fetch(`${NOTES_URL}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ title, content }),
  });
  if (!res.ok) throw new Error("Failed to update note");
  return res.json();
};

export const deleteNote = async (id) => {
  const res = await fetch(`${NOTES_URL}/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to delete note");
};

export const getDashboardStats = async () => {
  const res = await fetch(`${DASHBOARD_URL}/stats`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load dashboard");
  return res.json();
};

export const updateProfile = async (username) => {
  const res = await fetch(`${AUTH_URL}/profile`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ username }),
  });
  if (!res.ok) throw new Error("Failed to update profile");
  return res.json();
};

export const googleAuthUrl = `${AUTH_URL}/google`;

export const ocrDocument = async (file) => {
  const formData = new FormData();
  formData.append("file", file);
  const res = await fetch(`${DOC_URL}/ocr`, {
    method: "POST",
    body: formData,
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "OCR failed";
    try { msg = JSON.parse(body).error || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

const LEARNING_URL = `${API_BASE}/learning`;
const BOOKMARK_URL = `${API_BASE}/bookmarks`;

export const getLearningTree = async () => {
  const res = await fetch(`${LEARNING_URL}/tree`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load syllabus");
  return res.json();
};

export const getLearningTreeWithProgress = async () => {
  const res = await fetch(`${LEARNING_URL}/tree/progress`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load syllabus");
  return res.json();
};

export const getProgress = async () => {
  const res = await fetch(`${LEARNING_URL}/progress`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load progress");
  return res.json();
};

export const setTopicProgress = async (topicId, status) => {
  const res = await fetch(`${LEARNING_URL}/progress/${topicId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ status }),
  });
  if (!res.ok) throw new Error("Failed to update progress");
  return res.json();
};

export const generateSyllabus = async (exam, subjects) => {
  const res = await fetch(`${LEARNING_URL}/generate-syllabus`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ exam, subjects }),
  });
  if (!res.ok) throw new Error("Failed to generate syllabus");
  return res.json();
};

export const seedSyllabus = async (exam) => {
  const res = await fetch(`${LEARNING_URL}/seed-syllabus`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ exam }),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to load syllabus";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const getExams = async () => {
  const res = await fetch(`${LEARNING_URL}/exams`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load exams");
  return res.json();
};

export const createExam = async (exam) => {
  const res = await fetch(`${LEARNING_URL}/exams`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify(exam),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to create exam";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const deleteExam = async (id) => {
  const res = await fetch(`${LEARNING_URL}/exams/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to delete exam");
  return res.json();
};

export const deleteExamByName = async (name) => {
  const res = await fetch(`${LEARNING_URL}/exams/by-name/${encodeURIComponent(name)}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to delete exam";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

// ==================== Exam stats ====================

export const getExamStats = async () => {
  const res = await fetch(`${DASHBOARD_URL}/exam-stats`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load exam stats");
  return res.json();
};

// ==================== Review schedule (spaced repetition) ====================

export const getDueReviews = async () => {
  const res = await fetch(`${LEARNING_URL}/review/due`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load due reviews");
  return res.json();
};

export const getReviewSchedule = async () => {
  const res = await fetch(`${LEARNING_URL}/review/schedule`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load review schedule");
  return res.json();
};

export const addToReviewSchedule = async (topicId) => {
  const res = await fetch(`${LEARNING_URL}/review/${topicId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
  });
  if (!res.ok) throw new Error("Failed to add to review schedule");
  return res.json();
};

export const completeReview = async (topicId, quality = 4) => {
  const res = await fetch(`${LEARNING_URL}/review/${topicId}/complete`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ quality }),
  });
  if (!res.ok) throw new Error("Failed to complete review");
  return res.json();
};

export const removeFromReviewSchedule = async (topicId) => {
  const res = await fetch(`${LEARNING_URL}/review/${topicId}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to remove from review schedule");
  return res.json();
};

// ==================== Subject / Chapter CRUD ====================

export const addSubject = async (name, exam, description) => {
  const res = await fetch(`${LEARNING_URL}/subjects`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ name, exam, description }),
  });
  if (!res.ok) throw new Error("Failed to add subject");
  return res.json();
};

export const addChapter = async (subjectId, title) => {
  const res = await fetch(`${LEARNING_URL}/chapters`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ subjectId, title }),
  });
  if (!res.ok) throw new Error("Failed to add chapter");
  return res.json();
};

export const addTopic = async (chapterId, title, description) => {
  const res = await fetch(`${LEARNING_URL}/topics`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ chapterId, title, description }),
  });
  if (!res.ok) throw new Error("Failed to add topic");
  return res.json();
};

export const getTopic = async (id) => {
  const res = await fetch(`${LEARNING_URL}/topics/${id}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load topic");
  return res.json();
};

export const generateTopicContent = async (id) => {
  const res = await fetch(`${LEARNING_URL}/topics/${id}/generate-content`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: "{}",
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to generate content";
    try { msg = JSON.parse(body).message || msg; } catch { /* keep default */ }
    throw new Error(msg);
  }
  return res.json();
};

export const generateChapterSummary = async (id) => {
  const res = await fetch(`${LEARNING_URL}/chapters/${id}/generate-summary`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: "{}",
  });
  if (!res.ok) throw new Error("Failed to generate chapter summary");
  return res.json();
};

export const getTopicQuiz = async (id, count = 10, difficulty = "all") => {
  const res = await fetch(`${LEARNING_URL}/topics/${id}/quiz?count=${count}&difficulty=${difficulty}`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    let msg = "Failed to load quiz";
    try { msg = JSON.parse(body).message || (body || msg); } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const getTopicFlashcards = async (id, count = 100) => {
  const res = await fetch(`${LEARNING_URL}/topics/${id}/flashcards?count=${count}`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to load flashcards");
  return res.json();
};

export const getTopicReview = async (id) => {
  const res = await fetch(`${LEARNING_URL}/topics/${id}/review`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load topic questions");
  return res.json();
};

export const getChapterQuiz = async (id, count = 20, difficulty = "all") => {
  const res = await fetch(`${LEARNING_URL}/chapters/${id}/quiz?count=${count}&difficulty=${difficulty}`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to load chapter quiz";
    try { msg = JSON.parse(body).message || msg; } catch { /* keep default */ }
    throw new Error(msg);
  }
  return res.json();
};

export const getChapterReview = async (id) => {
  const res = await fetch(`${LEARNING_URL}/chapters/${id}/review`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load chapter questions");
  return res.json();
};

export const getBookmarks = async () => {
  const res = await fetch(BOOKMARK_URL, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load bookmarks");
  return res.json();
};

export const addBookmark = async (topicId) => {
  const res = await fetch(BOOKMARK_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeaders() },
    body: JSON.stringify({ topicId }),
  });
  if (!res.ok) throw new Error("Failed to bookmark topic");
  return res.json();
};

export const removeBookmark = async (topicId) => {
  const res = await fetch(`${BOOKMARK_URL}/${topicId}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to remove bookmark");
};

const PAPERS_URL = `${API_BASE}/papers`;

export const getPapers = async () => {
  const res = await fetch(PAPERS_URL, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load question papers");
  return res.json();
};

export const getPaper = async (id) => {
  const res = await fetch(`${PAPERS_URL}/${id}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load paper");
  return res.json();
};

export const uploadPaper = async (file, exam = null, year = null) => {
  const formData = new FormData();
  formData.append("file", file);
  if (exam) formData.append("exam", exam);
  if (year) formData.append("year", year);
  const res = await fetch(`${PAPERS_URL}/upload`, {
    method: "POST",
    body: formData,
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Upload failed";
    try { msg = JSON.parse(body).error || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const setPaperVisibility = async (id, isPublic) => {
  const res = await fetch(`${PAPERS_URL}/${id}/visibility`, {
    method: "PUT",
    headers: { ...getAuthHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify({ isPublic }),
  });
  if (!res.ok) throw new Error("Failed to update sharing");
  return res.json();
};

export const browsePapers = async (exam = null, year = null) => {
  const params = new URLSearchParams();
  if (exam) params.set("exam", exam);
  if (year) params.set("year", year);
  const qs = params.toString();
  const res = await fetch(`${PAPERS_URL}/browse${qs ? `?${qs}` : ""}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load shared papers");
  return res.json();
};

export const getPaperFilters = async () => {
  const res = await fetch(`${PAPERS_URL}/browse/filters`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load paper filters");
  return res.json();
};

export const getPaperQuiz = async (id, count = 0) => {
  const params = count > 0 ? `?count=${count}` : "";
  const res = await fetch(`${PAPERS_URL}/${id}/quiz${params}`, { headers: getAuthHeaders() });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to load quiz";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const deletePaper = async (id) => {
  const res = await fetch(`${PAPERS_URL}/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to delete paper");
};

const ADAPTIVE_URL = `${API_BASE}/adaptive`;

export const getAdaptiveInsights = async () => {
  const res = await fetch(`${ADAPTIVE_URL}/insights`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load insights");
  return res.json();
};

export const getAdaptivePlan = async () => {
  const res = await fetch(`${ADAPTIVE_URL}/plan`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load today's plan");
  return res.json();
};

export const getAdaptiveMistakes = async () => {
  const res = await fetch(`${ADAPTIVE_URL}/mistakes`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load mistakes");
  return res.json();
};

export const getAdaptiveQuiz = async (topic, count = 10) => {
  const res = await fetch(`${ADAPTIVE_URL}/quiz?topic=${encodeURIComponent(topic)}&count=${count}`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to load adaptive quiz";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const getAdaptiveDrill = async (count = 10) => {
  const res = await fetch(`${ADAPTIVE_URL}/drill?count=${count}`, { headers: getAuthHeaders() });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to build weak-topic drill";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

const TEMPLATES_URL = `${API_BASE}/quiz-templates`;

export const getQuizTemplates = async () => {
  const res = await fetch(TEMPLATES_URL, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error("Failed to load quiz templates");
  return res.json();
};

export const createQuizTemplate = async (data) => {
  const res = await fetch(TEMPLATES_URL, {
    method: "POST",
    headers: { ...getAuthHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to save template";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const updateQuizTemplate = async (id, data) => {
  const res = await fetch(`${TEMPLATES_URL}/${id}`, {
    method: "PUT",
    headers: { ...getAuthHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const body = await res.text();
    let msg = "Failed to update template";
    try { msg = JSON.parse(body).message || msg; } catch { msg = body || msg; }
    throw new Error(msg);
  }
  return res.json();
};

export const deleteQuizTemplate = async (id) => {
  const res = await fetch(`${TEMPLATES_URL}/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error("Failed to delete template");
};