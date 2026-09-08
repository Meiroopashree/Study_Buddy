import React from "react";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import remarkGfm from "remark-gfm";
import "katex/dist/katex.min.css";
import formatAIText from "../utils/formatAIText";
import "../styles/QuizQuestion.css";

const LETTERS = ["A", "B", "C", "D", "E", "F", "G", "H"];

export function normalizeType(type) {
  const t = String(type || "mcq").toLowerCase().replace(/[\s_-]/g, "");
  switch (t) {
    case "mcq":
    case "single":
    case "singlechoice":
    case "singlecorrect":
      return "mcq";
    case "multi":
    case "multiple":
    case "multiplecorrect":
    case "msq":
      return "multi";
    case "numerical":
    case "numeric":
    case "integer":
    case "integertype":
    case "short":
      return "numerical";
    case "assertion":
    case "assertionreason":
      return "assertion";
    case "matching":
    case "matchthefollowing":
    case "matchinglists":
      return "matching";
    case "truefalse":
    case "boolean":
    case "tf":
      return "truefalse";
    case "passage":
    case "comprehension":
    case "passagebased":
    case "paragraph":
      return "passage";
    default:
      return "mcq";
  }
}

function norm(s) {
  return String(s ?? "").trim().toLowerCase();
}

function correctSet(q) {
  const list = Array.isArray(q.correctAnswers) && q.correctAnswers.length
    ? q.correctAnswers
    : String(q.answer || "").split(",");
  return list.map((s) => String(s || "").trim()).filter(Boolean);
}

function selectedSet(answer) {
  return Array.isArray(answer) ? answer : (answer ? [answer] : []);
}

function parseMatching(answerStr) {
  const out = {};
  String(answerStr || "")
    .split(",")
    .forEach((part) => {
      const m = part.trim().match(/^([A-Za-z])\s*[-:]\s*(.+)$/);
      if (m) out[m[1].toUpperCase()] = m[2].trim();
    });
  return out;
}

export function isQuizAnswerCorrect(q, answer) {
  if (answer === undefined || answer === null || answer === "") return false;
  const type = normalizeType(q.type);
  switch (type) {
    case "multi": {
      const correct = correctSet(q).map(norm).filter(Boolean);
      const given = selectedSet(answer).map(norm).filter(Boolean);
      if (correct.length === 0) return false;
      if (given.length !== correct.length) return false;
      return correct.every((c) => given.includes(c));
    }
    case "numerical": {
      const a = parseFloat(norm(answer));
      const b = parseFloat(norm(q.answer));
      if (!Number.isNaN(a) && !Number.isNaN(b)) {
        const tol = Math.max(0.05, Math.abs(b) * 0.001);
        return Math.abs(a - b) <= tol;
      }
      return norm(answer) === norm(q.answer);
    }
    case "matching": {
      const expected = parseMatching(q.answer);
      const keys = Object.keys(expected);
      if (keys.length === 0) return false;
      const given = answer && typeof answer === "object" ? answer : {};
      return keys.every((k) => norm(given[k]) === norm(expected[k]));
    }
    default:
      return norm(answer) === norm(q.answer);
  }
}

export function formatQuizAnswer(q, answer) {
  if (answer === undefined || answer === null) return "";
  const type = normalizeType(q.type);
  if (type === "multi" && Array.isArray(answer)) return answer.join(", ");
  if (type === "matching" && typeof answer === "object") {
    return Object.keys(answer)
      .sort()
      .map((k) => `${k}-${answer[k]}`)
      .join(", ");
  }
  return String(answer);
}

function Md({ content }) {
  if (!content || !String(content).trim()) return null;
  return (
    <div className="markdown-body">
      <ReactMarkdown remarkPlugins={[remarkMath, remarkGfm]} rehypePlugins={[rehypeKatex]}>
        {formatAIText(String(content))}
      </ReactMarkdown>
    </div>
  );
}

export default function QuizQuestionBlock({
  question,
  answer,
  submitted,
  review,
  disabled,
  onAnswerChange,
}) {
  const q = question || {};
  const type = normalizeType(q.type);
  const options = Array.isArray(q.options) ? q.options : [];
  const rightOptions = Array.isArray(q.rightOptions) ? q.rightOptions : [];
  const showResult = !!submitted || !!review;
  const correct = correctSet(q);
  const sel = selectedSet(answer);

  const pick = (value) => {
    if (showResult || disabled) return;
    if (type === "multi") {
      const next = sel.includes(value) ? sel.filter((v) => v !== value) : [...sel, value];
      onAnswerChange && onAnswerChange(next);
    } else {
      onAnswerChange && onAnswerChange(value);
    }
  };

  const optionValue = (opt, idx) => (type === "assertion" ? LETTERS[idx] : opt);

  const isCorrectOption = (opt, idx) =>
    type === "assertion" ? norm(LETTERS[idx]) === norm(q.answer) : correct.includes(opt);

  const isSelectedOption = (opt, idx) =>
    type === "assertion" ? norm(sel[0]) === norm(LETTERS[idx]) : sel.includes(opt);

  const optionClass = (opt, idx) => {
    let cls = "qq-option";
    if (isSelectedOption(opt, idx)) cls += " selected";
    if (showResult) {
      if (isCorrectOption(opt, idx)) cls += " is-correct";
      else if (isSelectedOption(opt, idx)) cls += " is-wrong";
    }
    return cls;
  };

  const renderExplanation = () => {
    if (!showResult) return null;
    if (!q.explanation) return null;
    return (
      <div className="qq-explanation">
        <Md content={`**Explanation:** ${q.explanation}`} />
      </div>
    );
  };

  const renderPassage = () => {
    if (!q.passage || !String(q.passage).trim()) return null;
    return (
      <div className="qq-passage">
        <div className="qq-passage-label">Context</div>
        <Md content={q.passage} />
      </div>
    );
  };

  const renderOptions = () => {
    const opts = type === "truefalse" && options.length === 0 ? ["True", "False"] : options;
    if (opts.length === 0) return null;
    return (
      <div className="qq-options">
        {opts.map((opt, idx) => (
          <button
            key={idx}
            type="button"
            className={optionClass(opt, idx)}
            onClick={() => pick(optionValue(opt, idx))}
            disabled={showResult || disabled}
          >
            {type === "assertion" && <span className="qq-option-letter">{LETTERS[idx]}.</span>}
            <Md content={opt} />
            {showResult && isCorrectOption(opt, idx) && <span className="qq-badge">Correct</span>}
            {showResult && !isCorrectOption(opt, idx) && isSelectedOption(opt, idx) && (
              <span className="qq-badge qq-badge-wrong">Your answer</span>
            )}
          </button>
        ))}
      </div>
    );
  };

  if (type === "numerical") {
    return (
      <div className="qq-block">
        {renderPassage()}
        <input
          type="text"
          className="qq-numeric"
          inputMode="decimal"
          value={answer || ""}
          onChange={(e) => !showResult && !disabled && onAnswerChange && onAnswerChange(e.target.value)}
          placeholder="Type your numerical answer"
          disabled={showResult || disabled}
        />
        {showResult && (
          <div className="qq-feedback">
            <span className={isQuizAnswerCorrect(q, answer) ? "qq-feedback-correct" : "qq-feedback-wrong"}>
              Correct answer: {q.answer}
            </span>
          </div>
        )}
        {renderExplanation()}
      </div>
    );
  }

  if (type === "matching") {
    const matchVal = answer && typeof answer === "object" ? answer : {};
    const expected = parseMatching(q.answer);
    const listItems = options.length ? options : LETTERS.map((l) => `${l}`);
    return (
      <div className="qq-block">
        {renderPassage()}
        <div className="qq-matching">
          <div className="qq-matching-col">
            <div className="qq-matching-head">List-I</div>
            {listItems.map((item, r) => {
              const letter = LETTERS[r];
              const isOk = String(matchVal[letter] || "") === String(expected[letter] || "");
              return (
                <div className="qq-matching-row" key={r}>
                  <span className="qq-matching-letter">{letter}.</span>
                  <div className="qq-matching-item">
                    <Md content={item || ""} />
                  </div>
                  {!showResult ? (
                    <select
                      className="qq-matching-select"
                      value={matchVal[letter] || ""}
                      disabled={disabled}
                      onChange={(e) =>
                        onAnswerChange && onAnswerChange({ ...matchVal, [letter]: e.target.value })
                      }
                    >
                      <option value="">—</option>
                      {rightOptions.map((ro, c) => (
                        <option key={c} value={String(c + 1)}>
                          {c + 1}. {ro}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <span className={isOk ? "qq-match-ok" : "qq-match-no"}>
                      {expected[letter] !== undefined && <span>{letter}-{expected[letter]}</span>}
                      {matchVal[letter] !== undefined && !isOk && <span className="qq-match-yours">your: {matchVal[letter]}</span>}
                    </span>
                  )}
                </div>
              );
            })}
          </div>
          <div className="qq-matching-col">
            <div className="qq-matching-head">List-II</div>
            {rightOptions.map((ro, c) => (
              <div className="qq-matching-row qq-matching-static" key={c}>
                <span className="qq-matching-letter">{c + 1}.</span>
                <div className="qq-matching-item">
                  <Md content={ro} />
                </div>
              </div>
            ))}
          </div>
        </div>
        {showResult && !isQuizAnswerCorrect(q, answer) && (
          <div className="qq-feedback qq-feedback-wrong">Correct mapping: {q.answer}</div>
        )}
        {renderExplanation()}
      </div>
    );
  }

  if (type === "assertion") {
    return (
      <div className="qq-block">
        {renderPassage()}
        {q.assertion && (
          <div className="qq-assertion">
            <span className="qq-assertion-label">Assertion:</span>
            <Md content={q.assertion} />
          </div>
        )}
        {q.reason && (
          <div className="qq-assertion">
            <span className="qq-assertion-label">Reason:</span>
            <Md content={q.reason} />
          </div>
        )}
        {renderOptions()}
        {renderExplanation()}
      </div>
    );
  }

  return (
    <div className="qq-block">
      {renderPassage()}
      {renderOptions()}
      {renderExplanation()}
    </div>
  );
}
