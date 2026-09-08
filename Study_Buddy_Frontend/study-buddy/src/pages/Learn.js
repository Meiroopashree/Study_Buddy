import React, { useEffect, useMemo, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import { renderToStaticMarkup } from "react-dom/server";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import remarkGfm from "remark-gfm";
import "katex/dist/katex.min.css";
import LoadingSpinner from "../components/LoadingSpinner";
import {
  getLearningTreeWithProgress, getTopic, generateTopicContent,
  getTopicQuiz, getTopicFlashcards, getTopicReview, getChapterQuiz, getChapterReview,
  addBookmark, removeBookmark, getBookmarks, saveQuizResult,
  createExam, deleteExamByName, setTopicProgress,
  addToReviewSchedule, removeFromReviewSchedule,
  getQuizTemplates, createQuizTemplate, deleteQuizTemplate
} from "../services/api";
import formatAIText from "../utils/formatAIText";
import { downloadAsPdf, downloadAsMarkdown, downloadTextFile } from "../utils/exportContent";
import { useExam } from "../contexts/ExamContext";
import QuizQuestionBlock, { isQuizAnswerCorrect, formatQuizAnswer } from "../components/QuizQuestionBlock";
import "../styles/Learn.css";

const TABS = [
  { key: "lessonContent", label: "Lesson" },
  { key: "notesContent", label: "Notes" },
  { key: "revisionContent", label: "Revision" },
  { key: "formulaSheet", label: "Formula Sheet" },
  { key: "conceptMap", label: "Concept Map" },
];

const EXAM_META = {
  "JEE Main": { cls: "main" },
  "JEE Advanced": { cls: "advanced" },
};

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (c) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#39;",
  }[c]));
}

function Markdown({ content }) {
  if (!content || !content.trim()) {
    return <p className="muted">No content generated yet.</p>;
  }
  return (
    <div className="markdown-body">
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[rehypeKatex]}
      >
        {formatAIText(content)}
      </ReactMarkdown>
    </div>
  );
}

function Learn() {
  const { currentExam, setCurrentExam } = useExam();
  const [tree, setTree] = useState(null);
  const [error, setError] = useState("");

  const [selectedTopicId, setSelectedTopicId] = useState(null);
  const [topic, setTopic] = useState(null);
  const [activeTab, setActiveTab] = useState("lessonContent");
  const [generating, setGenerating] = useState(false);
  const [bookmarkedIds, setBookmarkedIds] = useState(new Set());
  const [collapsed, setCollapsed] = useState(() => new Set());
  const [searchTerm, setSearchTerm] = useState("");
  const [toast, setToast] = useState(null);
  const toastTimer = useRef(null);

  const showToast = (text, type = "success") => {
    if (toastTimer.current) clearTimeout(toastTimer.current);
    setToast({ text, type });
    toastTimer.current = setTimeout(() => setToast(null), 3000);
  };

  const collapseAll = () => {
    const keys = [];
    (tree || []).forEach((eg) => keys.push(`e:${eg.exam}`));
    (tree || []).forEach((eg) => (eg.subjects || []).forEach((s) => keys.push(`s:${s.id}`)));
    (tree || []).forEach((eg) => (eg.subjects || []).forEach((s) => (s.chapters || []).forEach((c) => keys.push(`c:${c.id}`))));
    setCollapsed(new Set(keys));
  };

  const expandAll = () => setCollapsed(new Set());

  const toggleCollapse = (key) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const [quizModal, setQuizModal] = useState(false);
  const [quizCount, setQuizCount] = useState(10);
  const [chapterQuizCount, setChapterQuizCount] = useState(20);
  const [quizScope, setQuizScope] = useState(null);
  const [quiz, setQuiz] = useState(null);
  const [quizLoading, setQuizLoading] = useState(false);
  const quizStartRef = useRef(null);

  const [reviewModal, setReviewModal] = useState(false);
  const [reviewTitle, setReviewTitle] = useState("");
  const [reviewData, setReviewData] = useState(null);
  const [reviewLoading, setReviewLoading] = useState(false);

  const [flashModal, setFlashModal] = useState(false);
  const [flashLoading, setFlashLoading] = useState(false);
  const [cards, setCards] = useState([]);
  const [flashIndex, setFlashIndex] = useState(0);
  const [flipped, setFlipped] = useState(false);

  const [quizAnswers, setQuizAnswers] = useState({});
  const [quizSubmitted, setQuizSubmitted] = useState(false);
  const [quizScore, setQuizScore] = useState(0);

  const [templates, setTemplates] = useState([]);
  const [templateName, setTemplateName] = useState("");
  const [templateMsg, setTemplateMsg] = useState("");
  const [templateSaved, setTemplateSaved] = useState(false);

  const loadTemplates = async () => {
    try {
      setTemplates(await getQuizTemplates());
    } catch {
      setTemplates([]);
    }
  };

  const handleSaveTemplate = async () => {
    if (!quizScope) return;
    const targetTitle = quizScope.type === "chapter" ? quizScope.title : topic?.title;
    if (!targetTitle) return;
    const count = quizScope.type === "chapter" ? chapterQuizCount : quizCount;
    const name = (templateName.trim() || `${targetTitle} (${count}Q)`).trim();
    setTemplateMsg("");
    try {
      await createQuizTemplate({
        name,
        scopeType: quizScope.type,
        exam: currentExam || null,
        subject: null,
        chapter: quizScope.type === "chapter" ? quizScope.title : null,
        topic: quizScope.type === "topic" ? targetTitle : null,
        questionCount: count,
        difficulty: "standard",
      });
      setTemplateName("");
      setTemplateSaved(true);
      setTimeout(() => setTemplateSaved(false), 2000);
      loadTemplates();
    } catch (e) {
      setTemplateMsg(e.message || "Could not save template.");
    }
  };

  const handleRunTemplate = async (tpl) => {
    if (tpl.scopeType === "chapter" && tpl.chapter) {
      const ch = await findChapterByTitle(tpl.chapter);
      if (ch) {
        openQuizModal({ type: "chapter", id: ch.id, title: ch.title });
        setChapterQuizCount(tpl.questionCount || 20);
      }
    } else if (tpl.scopeType === "topic" && tpl.topic) {
      const t = await findTopicByTitle(tpl.topic);
      if (t) {
        if (t.exam && t.exam !== currentExam) setCurrentExam(t.exam);
        await loadTopic(t.id);
        setQuizCount(tpl.questionCount || 10);
        openQuizModal({ type: "topic", id: t.id, title: t.topic });
      }
    } else if (tpl.exam && tpl.exam !== currentExam) {
      setCurrentExam(tpl.exam);
    }
  };

  const handleDeleteTemplate = async (e, id) => {
    e.stopPropagation();
    try {
      await deleteQuizTemplate(id);
      loadTemplates();
    } catch {
      /* ignore */
    }
  };

  const [examModal, setExamModal] = useState(false);
  const [examCreating, setExamCreating] = useState(false);
  const [deletingExam, setDeletingExam] = useState(null);
  const [examForm, setExamForm] = useState({ name: "", year: new Date().getFullYear(), subjects: "" });

  const [compareModal, setCompareModal] = useState(false);
  const [compareA, setCompareA] = useState("");
  const [compareB, setCompareB] = useState("");
  const [inReviewSchedule, setInReviewSchedule] = useState(new Set());

  const handleCreateExam = async (e) => {
    e.preventDefault();
    if (!examForm.name.trim()) return;
    setExamCreating(true);
    try {
      await createExam({
        name: examForm.name.trim(),
        year: Number(examForm.year) || new Date().getFullYear(),
        subjects: examForm.subjects.split(",").map((s) => s.trim()).filter(Boolean),
      });
      setExamModal(false);
      setExamForm({ name: "", year: new Date().getFullYear(), subjects: "" });
      await loadTree();
      showToast(`Exam "${examForm.name.trim()}" created — generating syllabus…`);
    } catch (err) {
      setError(err.message || "Failed to create exam");
    } finally {
      setExamCreating(false);
    }
  };

  const handleDeleteExam = async (name) => {
    if (!window.confirm(`Delete exam "${name}" and all its chapters and topics?`)) return;
    setDeletingExam(name);
    try {
      await deleteExamByName(name);
      await loadTree();
      showToast(`Deleted "${name}"`);
    } catch (err) {
      setError(err.message || "Failed to delete exam");
    } finally {
      setDeletingExam(null);
    }
  };

  const loadTree = async () => {
    try {
      const t = await getLearningTreeWithProgress();
      setTree(t);
      setError("");
    } catch (e) {
      setError(e.message || "Failed to load syllabus");
    }
  };

  useEffect(() => {
    loadTree();
    loadTemplates();
    getBookmarks()
      .then((b) => setBookmarkedIds(new Set((b || []).map((x) => x.topicId))))
      .catch(() => {});
  }, []);

  const loadTopic = async (id) => {
    setSelectedTopicId(id);
    setTopic(null);
    setActiveTab("lessonContent");
    setQuizAnswers({});
    setQuizSubmitted(false);
    try {
      const t = await getTopic(id);
      setTopic(t);
    } catch (e) {
      setError(e.message);
    }
  };

  const handleGenerateContent = async () => {
    if (!selectedTopicId) return;
    setGenerating(true);
    setError("");
    try {
      const t = await generateTopicContent(selectedTopicId);
      setTopic(t);
    } catch (e) {
      setError(e.message);
    } finally {
      setGenerating(false);
    }
  };

  const handleToggleBookmark = async (id) => {
    try {
      if (bookmarkedIds.has(id)) {
        await removeBookmark(id);
        const next = new Set(bookmarkedIds);
        next.delete(id);
        setBookmarkedIds(next);
      } else {
        await addBookmark(id);
        setBookmarkedIds(new Set(bookmarkedIds).add(id));
      }
    } catch (e) {
      setError(e.message);
    }
  };

  const handleSetStatus = async (topicId, status) => {
    try {
      await setTopicProgress(topicId, status);
      await loadTree();
      if (topic && topic.id === topicId) setTopic({ ...topic, status });
      showToast("Progress updated");
    } catch (e) {
      setError(e.message);
    }
  };

  const STATUS_LABELS = { not_started: "Not Started", in_progress: "In Progress", mastered: "Mastered", revising: "Revising" };

  const topicStatusOf = (t) => {
    if (t?.status) return t.status;
    for (const eg of tree || []) {
      for (const s of eg.subjects || []) {
        for (const c of s.chapters || []) {
          const m = (c.topics || []).find((tp) => tp.id === t?.id);
          if (m) return m.status || "not_started";
        }
      }
    }
    return "not_started";
  };

  const chapterProgress = (chapter) => {
    const topics = chapter.topics || [];
    if (topics.length === 0) return { done: 0, total: 0, pct: 0 };
    const done = topics.filter((t) => t.status === "mastered" || t.status === "in_progress" || t.status === "revising").length;
    return { done, total: topics.length, pct: Math.round((done / topics.length) * 100) };
  };

  const examProgress = (examGroup) => {
    const all = (examGroup.subjects || []).flatMap((s) => (s.chapters || []).flatMap((c) => c.topics || []));
    if (all.length === 0) return { done: 0, total: 0, pct: 0 };
    const done = all.filter((t) => t.status === "mastered" || t.status === "in_progress" || t.status === "revising").length;
    return { done, total: all.length, pct: Math.round((done / all.length) * 100) };
  };

  const examNames = (tree || []).map((eg) => eg.exam).filter(Boolean);

  const compareData = useMemo(() => {
    if (!compareA || !compareB || compareA === compareB) return null;
    const a = (tree || []).find((eg) => eg.exam === compareA);
    const b = (tree || []).find((eg) => eg.exam === compareB);
    if (!a || !b) return null;

    const subjectsA = new Map((a.subjects || []).map((s) => [s.name.toLowerCase(), s]));
    const subjectsB = new Map((b.subjects || []).map((s) => [s.name.toLowerCase(), s]));

    const shared = [];
    const onlyA = [];
    const onlyB = [];

    for (const [key, sA] of subjectsA) {
      if (subjectsB.has(key)) {
        const sB = subjectsB.get(key);
        const chaptersA = new Map((sA.chapters || []).map((c) => [c.title.toLowerCase(), c]));
        const chaptersB = new Map((sB.chapters || []).map((c) => [c.title.toLowerCase(), c]));
        const sharedChapters = [...chaptersA.keys()].filter((k) => chaptersB.has(k));
        const onlyChaptersA = [...chaptersA.keys()].filter((k) => !chaptersB.has(k));
        const onlyChaptersB = [...chaptersB.keys()].filter((k) => !chaptersA.has(k));
        shared.push({ subject: sA.name, sharedChapters: sharedChapters.length, onlyA: onlyChaptersA.length, onlyB: onlyChaptersB.length });
      } else {
        onlyA.push({ subject: sA.name, chapters: (sA.chapters || []).length });
      }
    }
    for (const [key, sB] of subjectsB) {
      if (!subjectsA.has(key)) {
        onlyB.push({ subject: sB.name, chapters: (sB.chapters || []).length });
      }
    }

    const totalShared = shared.reduce((sum, s) => sum + s.sharedChapters, 0);
    const totalA = (a.subjects || []).reduce((sum, s) => sum + (s.chapters || []).length, 0);
    const totalB = (b.subjects || []).reduce((sum, s) => sum + (s.chapters || []).length, 0);
    const overlapPct = totalA > 0 && totalB > 0 ? Math.round((totalShared / Math.max(totalA, totalB)) * 100) : 0;

    return { shared, onlyA, onlyB, totalShared, totalA, totalB, overlapPct };
  }, [tree, compareA, compareB]);

  const handleToggleReviewSchedule = async (topicId) => {
    try {
      if (inReviewSchedule.has(topicId)) {
        await removeFromReviewSchedule(topicId);
        const next = new Set(inReviewSchedule);
        next.delete(topicId);
        setInReviewSchedule(next);
        showToast("Removed from review schedule");
      } else {
        await addToReviewSchedule(topicId);
        setInReviewSchedule(new Set(inReviewSchedule).add(topicId));
        showToast("Added to review schedule");
      }
    } catch (e) {
      setError(e.message);
    }
  };

  const openQuizModal = (scope) => {
    const target = scope || (topic ? { type: "topic", id: topic.id, title: topic.title } : null);
    if (!target) return;
    setQuizScope(target);
    setQuizModal(true);
    setQuiz(null);
    setQuizLoading(false);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setError("");
  };

  const startQuiz = async () => {
    if (!quizScope) return;
    setQuizLoading(true);
    setError("");
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    const timeout = setTimeout(() => setQuizLoading(false), 120000);
    try {
      const q = quizScope.type === "chapter"
        ? await getChapterQuiz(quizScope.id, chapterQuizCount)
        : await getTopicQuiz(quizScope.id, quizCount);
      setQuiz(q);
      quizStartRef.current = Date.now();
    } catch (e) {
      setError(e.message || "AI could not generate the quiz. Please try again.");
    } finally {
      clearTimeout(timeout);
      setQuizLoading(false);
    }
  };

  const handleSubmitQuiz = () => {
    if (!quiz) return;
    let score = 0;
    quiz.questions.forEach((q, i) => {
      if (isQuizAnswerCorrect(q, quizAnswers[i])) score++;
    });
    setQuizScore(score);
    setQuizSubmitted(true);
    const result = {
      topic: quizScope?.title || quiz.topicTitle || "Quiz",
      difficulty: "mixed",
      score,
      totalQuestions: quiz.questions.length,
      timeSpentSeconds: quizStartRef.current ? Math.round((Date.now() - quizStartRef.current) / 1000) : 0,
      answers: quiz.questions.map((q, i) => ({
        question: q.questionText,
        yourAnswer: formatQuizAnswer(q, quizAnswers[i]) || null,
        correctAnswer: q.answer,
        isCorrect: isQuizAnswerCorrect(q, quizAnswers[i]),
      })),
      questions: quiz.questions,
    };
    saveQuizResult(result).catch(() => {});
  };

  const handleRetake = () => {
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
    quizStartRef.current = Date.now();
  };

  const handleNewQuiz = () => {
    setQuiz(null);
    setQuizAnswers({});
    setQuizSubmitted(false);
    setQuizScore(0);
  };

  const buildQuizMarkdown = () => {
    if (!quiz) return "";
    const lines = [`# Quiz: ${quizScope?.title || quiz.topicTitle || topic?.title || "Quiz"}`, ""];
    quiz.questions.forEach((q, i) => {
      const prompt = formatQuizAnswer(q, undefined).trim();
      lines.push(`**Q${i + 1}.** ${prompt}`);
      lines.push("");
      const yourAnswer = formatQuizAnswer(q, quizAnswers[i]);
      if (quizSubmitted || yourAnswer) {
        const isCorrect = isQuizAnswerCorrect(q, quizAnswers[i]);
        lines.push(`- Your answer: ${yourAnswer || "—"}`);
        lines.push(`- Result: ${isCorrect ? "Correct" : "Incorrect"}`);
        lines.push("");
      }
    });
    return lines.join("\n");
  };

  const handleExportQuizMd = () => {
    if (!quiz) return;
    const filename = `quiz-${(quizScope?.title || quiz.topicTitle || topic?.title || "quiz").replace(/[^a-z0-9]+/gi, "-").toLowerCase()}.md`;
    downloadTextFile(filename, buildQuizMarkdown());
  };

  const handleExportQuizPdf = () => {
    if (!quiz) return;
    downloadAsPdf({
      title: `Quiz: ${quizScope?.title || quiz.topicTitle || topic?.title || "Quiz"}`,
      subtitle: `${quiz.questions.length} questions · StudyBuddy`,
      markdown: buildQuizMarkdown(),
    });
  };

  const handleReview = async (scope) => {
    const target = scope || (topic ? { type: "topic", id: topic.id, title: topic.title } : null);
    if (!target) return;
    setReviewModal(true);
    setReviewTitle(target.title);
    setReviewData(null);
    setReviewLoading(true);
    try {
      const data = target.type === "chapter"
        ? await getChapterReview(target.id)
        : await getTopicReview(target.id);
      setReviewData(data);
    } catch (e) {
      setError(e.message);
      setReviewModal(false);
    } finally {
      setReviewLoading(false);
    }
  };

  const handleFlashcards = async () => {
    if (!selectedTopicId) return;
    setFlashModal(true);
    setFlashLoading(true);
    setCards([]);
    setFlashIndex(0);
    setFlipped(false);
    try {
      const f = await getTopicFlashcards(selectedTopicId);
      setCards(f.cards || []);
    } catch (e) {
      setError(e.message);
    } finally {
      setFlashLoading(false);
    }
  };

  const hasTopicContent = useMemo(() => {
    if (!topic) return false;
    return TABS.some((t) => (topic[t.key] || "").trim().length > 0);
  }, [topic]);

  const filteredTree = useMemo(() => {
    let source = tree || [];
    if (currentExam) source = source.filter((eg) => eg.exam === currentExam);
    const q = searchTerm.trim().toLowerCase();
    if (!q) return source;
    return source
      .map((eg) => ({
        ...eg,
        subjects: (eg.subjects || [])
          .map((s) => ({
            ...s,
            chapters: (s.chapters || [])
              .map((c) => ({
                ...c,
                topics: (c.topics || []).filter((t) =>
                  t.title.toLowerCase().includes(q) || c.title.toLowerCase().includes(q) || s.name.toLowerCase().includes(q)
                ),
              }))
              .filter((c) => c.topics.length > 0),
          }))
          .filter((s) => s.chapters.length > 0),
      }))
      .filter((eg) => eg.subjects.length > 0);
  }, [tree, searchTerm, currentExam]);

  const findChapterByTitle = async (title) => {
    if (!title) return null;
    const source = tree || [];
    for (const eg of source) {
      for (const s of eg.subjects || []) {
        for (const c of s.chapters || []) {
          if (c.title.trim().toLowerCase() === title.trim().toLowerCase()) return c;
        }
      }
    }
    return null;
  };

  const findTopicByTitle = async (title) => {
    if (!title) return null;
    const source = tree || [];
    for (const eg of source) {
      for (const s of eg.subjects || []) {
        for (const c of s.chapters || []) {
          for (const t of c.topics || []) {
            if (t.title.trim().toLowerCase() === title.trim().toLowerCase()) {
              return { id: t.id, topic: t.title, exam: eg.exam };
            }
          }
        }
      }
    }
    return null;
  };

  const handleDownloadFormulaSheet = () => {
    if (!topic?.formulaSheet?.trim()) return;
    const title = topic.title || "Formula Sheet";
    const chapter = topic.chapter?.title || "";
    const body = renderToStaticMarkup(
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[[rehypeKatex, { output: "mathml", throwOnError: false }]]}
      >
        {formatAIText(topic.formulaSheet)}
      </ReactMarkdown>
    );
    const doc = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<title>${escapeHtml(title)} - Formula Sheet</title>
<style>
  @page { size: A4; margin: 15mm; }
  * { box-sizing: border-box; }
  body { font-family: system-ui, -apple-system, "Segoe UI", Roboto, Arial, sans-serif; max-width: 780px; margin: 0 auto; color: #1a202c; line-height: 1.65; }
  h1 { font-size: 1.5rem; margin: 0 0 2px; }
  h2 { font-size: 1.1rem; margin: 1.3em 0 0.45em; color: #1d4ed8; break-after: avoid; }
  h3 { font-size: 1rem; margin: 1.1em 0 0.35em; break-after: avoid; }
  p { margin: 0.5em 0; }
  ul, ol { padding-left: 1.5em; }
  table { border-collapse: collapse; margin: 10px 0; width: 100%; }
  th, td { border: 1px solid #cbd5e1; padding: 6px 10px; text-align: left; }
  math { font-family: "Cambria Math", "STIX Two Math", "Latin Modern Math", serif; }
  blockquote { border-left: 3px solid #cbd5e1; margin: 1em 0; padding-left: 1em; color: #475569; }
  code { background: #f1f5f9; padding: 2px 5px; border-radius: 4px; font-size: 0.9em; }
  pre { background: #f8fafc; border: 1px solid #e2e8f0; padding: 12px; overflow-x: auto; border-radius: 6px; }
  .sheet-header { border-bottom: 2px solid #0a3339; padding-bottom: 10px; margin-bottom: 1.4em; }
  .sheet-chapter { color: #64748b; font-size: 0.95rem; }
  .sheet-footer { margin-top: 2em; padding-top: 1em; border-top: 1px solid #e2e8f0; color: #94a3b8; font-size: 0.8rem; }
  @media print {
    h1, h2, h3 { break-after: avoid; }
  }
</style>
</head>
<body>
<div class="sheet-header">
  <h1>${escapeHtml(title)}</h1>
  <div class="sheet-chapter">${chapter ? `Chapter: ${escapeHtml(chapter)}` : "Formula Sheet"}</div>
</div>
${body}
<p class="sheet-footer">Generated by StudyBuddy</p>
</body>
</html>`;
    const win = window.open("", "_blank");
    if (!win) {
      setError("Popup blocked. Allow popups for this site, then try again.");
      return;
    }
    win.document.open();
    win.document.write(doc);
    win.document.close();
    win.focus();
    setTimeout(() => {
      win.print();
    }, 150);
  };

  const handleDownloadActiveTab = () => {
    const raw = topic?.[activeTab];
    if (!raw || !String(raw).trim()) return;
    const title = topic?.title || "Study Material";
    const label = TABS.find((t) => t.key === activeTab)?.label || "Content";
    const subtitle = `Topic: ${title}`;
    downloadAsPdf({ title: `${title} - ${label}`, subtitle, markdown: String(raw) });
  };

  const handleDownloadActiveTabMd = () => {
    const raw = topic?.[activeTab];
    if (!raw || !String(raw).trim()) return;
    const title = topic?.title || "Study Material";
    const label = TABS.find((t) => t.key === activeTab)?.label || "Content";
    downloadAsMarkdown({
      filename: `${title.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}-${label.toLowerCase().replace(/\s+/g, "-")}.md`,
      title: `${title} - ${label}`,
      subtitle: `Topic: ${title}`,
      markdown: String(raw),
    });
  };

  return (
    <div className="learn-container">
      <aside className="learn-sidebar">
        <div className="learn-sidebar-header">
          <h2>Syllabus</h2>
          {tree && tree.length > 0 && (
            <button
              className="btn btn-sm btn-ghost"
              onClick={() => (collapsed.size > 0 ? expandAll : collapseAll)()}
              title="Expand or collapse the entire syllabus"
            >
              {collapsed.size > 0 ? "⊞ Expand" : "⊟ Collapse"}
            </button>
          )}
          <button className="btn btn-sm btn-primary" onClick={() => setExamModal(true)}>
            + Create Exam
          </button>
          {examNames.length >= 2 && (
            <button className="btn btn-sm btn-ghost" onClick={() => setCompareModal(true)}>
              Compare
            </button>
          )}
        </div>

        <div className="learn-search">
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search exam, subject, chapter or topic…"
          />
          {searchTerm && (
            <button className="learn-search-clear" onClick={() => setSearchTerm("")} title="Clear search">✕</button>
          )}
        </div>

        <div className="learn-status-legend">
          <span className="legend-item"><span className="learn-status-dot learn-status-not_started" />Not started</span>
          <span className="legend-item"><span className="learn-status-dot learn-status-in_progress" />In progress</span>
          <span className="legend-item"><span className="learn-status-dot learn-status-mastered" />Mastered</span>
          <span className="legend-item"><span className="learn-status-dot learn-status-revising" />Revising</span>
        </div>

        <div className="learn-tree">
          {!tree ? (
            <LoadingSpinner message="Loading your syllabus…" />
          ) : tree.length === 0 ? (
            <div className="learn-tree-empty">
              <p className="muted">No syllabus yet.</p>
              <button className="btn btn-sm btn-primary" onClick={() => setExamModal(true)}>
                + Create an exam to get started
              </button>
            </div>
          ) : filteredTree.length === 0 ? (
            <p className="muted">
              {searchTerm ? `No matches for "${searchTerm}".` : "No syllabus for the selected exam — pick a different exam above."}
            </p>
          ) : (
            filteredTree.map((examGroup) => {
              const examKey = `e:${examGroup.exam}`;
              const examOpen = !collapsed.has(examKey);
              const meta = EXAM_META[examGroup.exam] || { cls: "custom" };

              return (
                <div
                  className={`learn-exam learn-exam--${meta.cls}`}
                  key={examGroup.exam}
                >
                  <div className="learn-exam-head">
                    <div
                      className="learn-exam-name learn-toggle"
                      onClick={() => toggleCollapse(examKey)}
                    >
                      <span className="learn-chevron">{examOpen ? "▾" : "▸"}</span>
                      {examGroup.exam}
                    </div>
                    <button
                      className="learn-exam-delete"
                      title={`Delete ${examGroup.exam}`}
                      disabled={deletingExam === examGroup.exam}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDeleteExam(examGroup.exam);
                      }}
                    >
                      {deletingExam === examGroup.exam ? "Delete…" : "✕ Delete"}
                    </button>
                  </div>
                  {(() => {
                    const p = examProgress(examGroup);
                    return p.total > 0 ? (
                      <div className="learn-progress learn-progress-exam">
                        <span className="learn-progress-label">
                          {p.done}/{p.total} · {p.pct}%
                        </span>
                        <div className="learn-progress-track">
                          <div className="learn-progress-fill" style={{ width: `${p.pct}%` }} />
                        </div>
                      </div>
                    ) : null;
                  })()}
                  {examOpen &&
                    examGroup.subjects.map((subject) => {
                      const subjectKey = `s:${subject.id}`;
                      const subjectOpen = !collapsed.has(subjectKey);
                      return (
                        <div className="learn-subject" key={subject.id}>
                          <div
                            className="learn-subject-name learn-toggle"
                            onClick={() => toggleCollapse(subjectKey)}
                          >
                            <span className="learn-chevron">{subjectOpen ? "▾" : "▸"}</span>
                            {subject.name}
                          </div>
                          {subjectOpen &&
                            subject.chapters.map((chapter) => {
                              const chapterKey = `c:${chapter.id}`;
                              const chapterOpen = !collapsed.has(chapterKey);
                              return (
                                <div className="learn-chapter" key={chapter.id}>
                                  <div className="learn-chapter-row">
                                    <div
                                      className="learn-chapter-name learn-toggle"
                                      onClick={() => toggleCollapse(chapterKey)}
                                    >
                                      <span className="learn-chevron">{chapterOpen ? "▾" : "▸"}</span>
                                      <span className="learn-chapter-title">{chapter.title}</span>
                                    </div>
                                    <div className="learn-chapter-actions">
                                      <button
                                        className="learn-mini-btn"
                                        title={`Take a quiz covering all topics in ${chapter.title}`}
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          openQuizModal({ type: "chapter", id: chapter.id, title: chapter.title });
                                        }}
                                      >
                                        Quiz
                                      </button>
                                      <button
                                        className="learn-mini-btn"
                                        title={`Review questions for ${chapter.title}`}
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          handleReview({ type: "chapter", id: chapter.id, title: chapter.title });
                                        }}
                                      >
                                        Review
                                      </button>
                                    </div>
                                  </div>
                                  {(() => {
                                    const cp = chapterProgress(chapter);
                                    return cp.total > 0 ? (
                                      <div className="learn-progress learn-progress-chapter">
                                        <span className="learn-progress-label">
                                          {cp.done}/{cp.total} · {cp.pct}%
                                        </span>
                                        <div className="learn-progress-track">
                                          <div className="learn-progress-fill" style={{ width: `${cp.pct}%` }} />
                                        </div>
                                      </div>
                                    ) : null;
                                  })()}
                                  {chapterOpen &&
                                    chapter.topics.map((topic) => (
                                      <div
                                        key={topic.id}
                                        className={`learn-topic ${selectedTopicId === topic.id ? "active" : ""}`}
                                        onClick={() => loadTopic(topic.id)}
                                      >
                                        <span className={`learn-status-dot learn-status-${topic.status || "not_started"}`}
                                          title={(STATUS_LABELS[topic.status] || "Not Started") + " — click topic to change"} />
                                        <span className="learn-topic-title">{topic.title}</span>
                                        {bookmarkedIds.has(topic.id) && (
                                          <span className="learn-bookmark-dot">★</span>
                                        )}
                                      </div>
                                    ))}
                                </div>
                              );
                            })}
                        </div>
                      );
                    })}
                </div>
              );
            })
          )}
        </div>
      </aside>

      <main className="learn-main">
        {!topic ? (
          <div className="learn-empty">
            <h2>Select a topic to start learning</h2>
            <p className="muted">
              Choose a topic from the syllabus tree, then generate study material,
              take a quiz, or practice with flashcards.
            </p>
          </div>
        ) : (
          <>
            <div className="learn-topic-header">
              <div>
                <h2>{topic.title}</h2>
                {topic.description && <p className="muted">{topic.description}</p>}
                {topic.chapter && (
                  <p className="muted">Chapter: {topic.chapter.title}</p>
                )}
              </div>
              <div className="learn-actions">
                <label className="learn-status-select">
                  <span>Status</span>
                  <select value={topicStatusOf(topic)} onChange={(e) => handleSetStatus(topic.id, e.target.value)}>
                    <option value="not_started">Not Started</option>
                    <option value="in_progress">In Progress</option>
                    <option value="mastered">Mastered</option>
                    <option value="revising">Revising</option>
                  </select>
                </label>
                <button
                  className="btn"
                  onClick={() => handleToggleBookmark(topic.id)}
                  title="Bookmark topic"
                >
                  {bookmarkedIds.has(topic.id) ? "★ Bookmarked" : "☆ Bookmark"}
                </button>
                <button className="btn btn-primary" onClick={handleGenerateContent} disabled={generating}>
                  {generating ? "Generating..." : hasTopicContent ? "Regenerate" : "Generate Content"}
                </button>
                <button className="btn" onClick={() => openQuizModal()}>Take Quiz</button>
                <button className="btn" onClick={() => handleReview()}>Review</button>
                <button className="btn" onClick={handleFlashcards}>Flashcards</button>
              </div>
            </div>

            {error && <p className="auth-error">{error}</p>}

            {!hasTopicContent && !generating ? (
              <div className="learn-empty">
                <p className="muted">
                  No study content yet. Click "Generate Content" to create the lesson,
                  notes, revision, formula sheet, and concept map for this topic.
                </p>
              </div>
            ) : (
              <>
                <div className="learn-tabs">
                  {TABS.map((tab) => (
                    <button
                      key={tab.key}
                      className={`learn-tab ${activeTab === tab.key ? "active" : ""}`}
                      onClick={() => setActiveTab(tab.key)}
                    >
                      {tab.label}
                    </button>
                  ))}
                  {activeTab === "formulaSheet" && (topic.formulaSheet || "").trim() && (
                    <button
                      className="btn btn-sm learn-tab-download"
                      onClick={handleDownloadFormulaSheet}
                      title="Opens a print dialog to save the formula sheet as a PDF"
                    >
                      Download PDF
                    </button>
                  )}
                  {activeTab !== "formulaSheet" && topic?.[activeTab] && String(topic[activeTab]).trim() && (
                    <>
                      <button
                        className="btn btn-sm learn-tab-download"
                        onClick={handleDownloadActiveTab}
                        title="Opens a print dialog to save this section as a PDF"
                      >
                        Download PDF
                      </button>
                      <button
                        className="btn btn-sm learn-tab-download"
                        onClick={handleDownloadActiveTabMd}
                        title="Download this section as a Markdown file"
                      >
                        .md
                      </button>
                    </>
                  )}
                </div>
                <div className="learn-content">
                  {generating && activeTab === "lessonContent" && !topic[activeTab] ? (
                    <LoadingSpinner
                      messages={[
                        "Generating your study material…",
                        "Writing the detailed lesson…",
                        "Creating notes & revision…",
                        "Building the formula sheet…",
                        "Drawing the concept map…",
                      ]}
                    />
                  ) : (
                    <Markdown content={topic[activeTab]} />
                  )}
                </div>
              </>
            )}
          </>
        )}
      </main>

      {quizModal && (
        <div className="modal-overlay" onClick={() => setQuizModal(false)}>
          <div className="modal-card learn-quiz-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Quiz: {quizScope?.title || topic?.title}</h3>
              <button className="close-btn" onClick={() => setQuizModal(false)} aria-label="Close">×</button>
            </div>

            {quizLoading && !quiz ? (
              <LoadingSpinner
                messages={[
                  "Generating your quiz…",
                  "Reading the topic content…",
                  "Crafting medium & hard questions…",
                  "Polishing the question set…",
                ]}
              />
            ) : !quiz ? (
              <>
                <div className="quiz-config">
                  {quizScope?.type === "chapter" ? (
                    <>
                      <label>
                        Target questions
                        <select value={chapterQuizCount} onChange={(e) => setChapterQuizCount(Number(e.target.value))}>
                          {[10, 20, 30, 40].map((n) => <option key={n} value={n}>{n}</option>)}
                        </select>
                      </label>
                      <p className="muted quiz-config-hint">
                        Covers every sub-topic in this chapter in easy, medium &amp; hard — with no duplicate questions.
                      </p>
                    </>
                  ) : (
                    <>
                      <label>
                        Target questions
                        <select value={quizCount} onChange={(e) => setQuizCount(Number(e.target.value))}>
                          {[5, 10, 15, 20, 25, 30].map((n) => <option key={n} value={n}>{n}</option>)}
                        </select>
                      </label>
                      <p className="muted quiz-config-hint">
                        Covers every concept from the lesson in easy, medium &amp; hard — with no duplicate questions.
                      </p>
                    </>
                  )}
                  <button className="btn btn-primary" onClick={startQuiz} disabled={quizLoading}>
                    {quizLoading ? "Generating..." : "Start Quiz"}
                  </button>
                  <div className="quiz-template-box">
                    <div className="quiz-template-save">
                      <input
                        type="text"
                        placeholder="Template name (e.g. NEET Physics - Atoms)"
                        value={templateName}
                        onChange={(e) => setTemplateName(e.target.value)}
                      />
                      <button className="btn btn-sm btn-ghost" onClick={handleSaveTemplate}>
                        Save as template
                      </button>
                      {templateSaved && <span className="template-ok">Saved</span>}
                      {templateMsg && <span className="auth-error">{templateMsg}</span>}
                    </div>
                    {templates.length > 0 && (
                      <div className="quiz-template-list">
                        <p className="muted">My templates</p>
                        {templates.map((tpl) => (
                          <div className="quiz-template-row" key={tpl.id}>
                            <button
                              className="quiz-template-run"
                              onClick={() => handleRunTemplate(tpl)}
                              title={`Re-run: ${tpl.name} (${tpl.questionCount} questions)`}
                            >
                              <span className="quiz-template-name">{tpl.name}</span>
                              <span className="muted">
                                {tpl.exam ? `${tpl.exam} · ` : ""}{tpl.questionCount}Q
                              </span>
                            </button>
                            <button
                              className="btn btn-sm btn-danger"
                              onClick={(e) => handleDeleteTemplate(e, tpl.id)}
                              title="Delete template"
                            >
                              Delete
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                  {error && <p className="auth-error quiz-config-error">{error}</p>}
                </div>
              </>
            ) : quizSubmitted ? (
              <>
                <div className="quiz-result">
                  <h2>{quizScore} / {quiz.questions.length}</h2>
                  <p className="muted">
                    Accuracy: {Math.round((quizScore / quiz.questions.length) * 100)}%
                  </p>
                  <div className="quiz-result-actions">
                    <button className="btn" onClick={handleNewQuiz}>New Quiz</button>
                    <button className="btn btn-primary" onClick={handleRetake}>Retake</button>
                    <button className="btn" onClick={handleExportQuizPdf} title="Save this quiz as a PDF">PDF</button>
                    <button className="btn" onClick={handleExportQuizMd} title="Download this quiz as a Markdown file">.md</button>
                  </div>
                </div>
                <div className="quiz-body">
                  {quiz.questions.map((q, i) => (
                    <div
                      className={`quiz-question ${isQuizAnswerCorrect(q, quizAnswers[i]) ? "is-correct" : "is-wrong"}`}
                      key={i}
                    >
                      <Markdown content={`**Q${i + 1}.** ${q.questionText}`} />
                      <QuizQuestionBlock question={q} answer={quizAnswers[i]} submitted />
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <>
                <div className="quiz-body">
                  {quiz.questions.map((q, i) => (
                    <div className="quiz-question" key={i}>
                      <Markdown content={`**Q${i + 1}.** ${q.questionText}`} />
                      <QuizQuestionBlock
                        question={q}
                        answer={quizAnswers[i]}
                        onAnswerChange={(v) => setQuizAnswers((prev) => ({ ...prev, [i]: v }))}
                      />
                    </div>
                  ))}
                </div>
                <div className="modal-footer">
                  <button className="btn btn-primary" onClick={handleSubmitQuiz}>
                    Submit Quiz
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}

      {flashModal && (
        <div className="modal-overlay" onClick={() => setFlashModal(false)}>
          <div className="modal-card learn-flash-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Flashcards: {topic?.title}</h3>
              <div className="modal-header-actions">
                {selectedTopicId && (
                  <button
                    className={`btn btn-sm ${inReviewSchedule.has(selectedTopicId) ? "btn-ghost" : "btn-primary"}`}
                    onClick={() => handleToggleReviewSchedule(selectedTopicId)}
                  >
                    {inReviewSchedule.has(selectedTopicId) ? "Scheduled ✓" : "+ Schedule Review"}
                  </button>
                )}
                <button className="close-btn" onClick={() => setFlashModal(false)} aria-label="Close">×</button>
              </div>
            </div>
            <div className="flash-body">
              {flashLoading ? (
                <LoadingSpinner
                  size="inline"
                  message="Loading your flashcards…"
                />
              ) : cards.length === 0 ? (
                <p className="muted">No flashcards yet. Take a quiz first to create them.</p>
              ) : (
                <>
                  <div
                    className={`flash-card ${flipped ? "flipped" : ""}`}
                    onClick={() => setFlipped((v) => !v)}
                  >
                    <div className="flash-inner">
                      <div className="flash-face flash-front">
                        <Markdown content={cards[flashIndex].front} />
                        <span className="flash-hint">Click to flip</span>
                      </div>
                      <div className="flash-face flash-back">
                        <Markdown content={cards[flashIndex].back} />
                      </div>
                    </div>
                  </div>
                  <div className="flash-controls">
                    <span className="muted">{flashIndex + 1} / {cards.length}</span>
                    <div>
                      <button
                        className="btn btn-sm"
                        disabled={flashIndex === 0}
                        onClick={() => { setFlashIndex((i) => i - 1); setFlipped(false); }}
                      >
                        Prev
                      </button>
                      <button
                        className="btn btn-sm"
                        disabled={flashIndex === cards.length - 1}
                        onClick={() => { setFlashIndex((i) => i + 1); setFlipped(false); }}
                      >
                        Next
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {examModal && (
        <div className="modal-overlay" onClick={() => setExamModal(false)}>
          <div className="modal-card learn-exam-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Create Exam</h3>
              <button className="close-btn" onClick={() => setExamModal(false)} aria-label="Close">×</button>
            </div>
            <form className="exam-form" onSubmit={handleCreateExam}>
              <label>
                Exam Name
                <input
                  type="text"
                  placeholder="e.g. NEET, MHT-CET ..."
                  value={examForm.name}
                  onChange={(e) => setExamForm((f) => ({ ...f, name: e.target.value }))}
                  required
                />
              </label>
              <label>
                Year
                <input
                  type="number"
                  value={examForm.year}
                  onChange={(e) => setExamForm((f) => ({ ...f, year: e.target.value }))}
                />
              </label>
              <label>
                Subjects (comma-separated)
                <input
                  type="text"
                  placeholder="e.g. Physics, Chemistry, Biology"
                  value={examForm.subjects}
                  onChange={(e) => setExamForm((f) => ({ ...f, subjects: e.target.value }))}
                />
              </label>
              <div className="modal-footer">
                <button className="btn btn-primary" disabled={examCreating}>
                  {examCreating ? "Creating..." : "Create"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {compareModal && (
        <div className="modal-overlay" onClick={() => setCompareModal(false)}>
          <div className="modal-card learn-compare-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Compare Exams</h3>
              <button className="close-btn" onClick={() => setCompareModal(false)} aria-label="Close">×</button>
            </div>
            <div className="compare-selectors">
              <label>
                Exam A
                <select value={compareA} onChange={(e) => setCompareA(e.target.value)}>
                  <option value="">Select…</option>
                  {examNames.map((n) => <option key={n} value={n}>{n}</option>)}
                </select>
              </label>
              <span className="compare-vs">vs</span>
              <label>
                Exam B
                <select value={compareB} onChange={(e) => setCompareB(e.target.value)}>
                  <option value="">Select…</option>
                  {examNames.map((n) => <option key={n} value={n}>{n}</option>)}
                </select>
              </label>
            </div>
            {compareData ? (
              <div className="compare-body">
                <div className="compare-summary">
                  <span className="compare-overlap">{compareData.overlapPct}% chapter overlap</span>
                  <span className="compare-detail">{compareData.totalShared} shared / {compareData.totalA} in {compareA} / {compareData.totalB} in {compareB}</span>
                </div>
                {compareData.shared.length > 0 && (
                  <div className="compare-section">
                    <h4>Shared subjects</h4>
                    <ul>
                      {compareData.shared.map((s) => (
                        <li key={s.subject}>
                          <strong>{s.subject}</strong> — {s.sharedChapters} shared chapters
                          {s.onlyA > 0 && <span className="compare-only"> · {s.onlyA} only in {compareA}</span>}
                          {s.onlyB > 0 && <span className="compare-only"> · {s.onlyB} only in {compareB}</span>}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {compareData.onlyA.length > 0 && (
                  <div className="compare-section">
                    <h4>Only in {compareA}</h4>
                    <ul>
                      {compareData.onlyA.map((s) => (
                        <li key={s.subject}><strong>{s.subject}</strong> ({s.chapters} chapters)</li>
                      ))}
                    </ul>
                  </div>
                )}
                {compareData.onlyB.length > 0 && (
                  <div className="compare-section">
                    <h4>Only in {compareB}</h4>
                    <ul>
                      {compareData.onlyB.map((s) => (
                        <li key={s.subject}><strong>{s.subject}</strong> ({s.chapters} chapters)</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            ) : (
              <p className="muted" style={{ padding: 16 }}>
                Select two different exams above to compare their syllabi.
              </p>
            )}
          </div>
        </div>
      )}

      {reviewModal && (
        <div className="modal-overlay" onClick={() => setReviewModal(false)}>
          <div className="modal-card learn-review-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Review: {reviewTitle}</h3>
              <button className="close-btn" onClick={() => setReviewModal(false)} aria-label="Close">×</button>
            </div>
            <div className="review-body">
              {reviewLoading ? (
                <LoadingSpinner
                  size="inline"
                  message="Loading your revision questions…"
                />
              ) : !reviewData || !reviewData.questions || reviewData.questions.length === 0 ? (
                <p className="muted">
                  No saved questions yet. Take a quiz for this topic or chapter to build up your revision bank.
                </p>
              ) : (
                <>
                  <p className="muted">
                    {reviewData.questionCount} question{reviewData.questionCount === 1 ? "" : "s"} in your revision bank
                    {reviewData.chapterTitle ? ` · ${reviewData.chapterTitle}` : ""}.
                  </p>
                  <div className="review-list">
                    {reviewData.questions.map((q, i) => (
                      <div className="review-question" key={q.id ?? i}>
                        <div className="review-q-head">
                          <span className="review-q-num">Q{i + 1}</span>
                          {q.topic && <span className="review-topic-tag">{q.topic}</span>}
                        </div>
                        <Markdown content={`**${q.questionText}**`} />
                        <QuizQuestionBlock question={q} review />
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className={`learn-toast learn-toast--${toast.type}`}>
          {toast.text}
        </div>
      )}
    </div>
  );
}

export default Learn;
