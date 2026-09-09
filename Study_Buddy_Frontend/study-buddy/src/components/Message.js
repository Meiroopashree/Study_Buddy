import React from "react";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import remarkGfm from "remark-gfm";
import rehypeKatex from "rehype-katex";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { oneDark } from "react-syntax-highlighter/dist/esm/styles/prism";
import "katex/dist/katex.min.css";
import formatAIText from "../utils/formatAIText";
import S from "./icons";
import "../styles/Message.css";

function CodeBlock({ className, children }) {
  const match = /language-(\w+)/.exec(className || "");
  const code = String(children).replace(/\n$/, "");
  return match ? (
    <SyntaxHighlighter style={oneDark} language={match[1]} PreTag="div">
      {code}
    </SyntaxHighlighter>
  ) : (
    <code className="inline-code">{code}</code>
  );
}

function Message({ text, type, streaming, onSaveNote }) {
  const formatted = type === "ai" ? formatAIText(text) : text;

  if (type === "ai") {
    return (
      <div className="message ai">
        <ReactMarkdown
          remarkPlugins={[remarkMath, remarkGfm]}
          rehypePlugins={[rehypeKatex]}
          components={{ code: CodeBlock }}
        >
          {formatted || ""}
        </ReactMarkdown>
        {streaming && <span className="streaming-cursor">▍</span>}
        {onSaveNote && !streaming && (
          <button className="save-note-btn" onClick={onSaveNote} title="Save to notes">
            <S.bookmark size={14} />Save
          </button>
        )}
      </div>
    );
  }

  return <div className="message user">{text}</div>;
}

export default Message;