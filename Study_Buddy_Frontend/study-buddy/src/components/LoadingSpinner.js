import React, { useEffect, useState } from "react";
import "../styles/LoadingSpinner.css";

const defaultMessages = [
  "Preparing your study material…",
  "Loading your progress…",
  "Warming up the AI…",
  "Almost there…",
  "Setting up your workspace…",
];

export default function LoadingSpinner({
  size = "full",
  messages = defaultMessages,
  message,
}) {
  const [idx, setIdx] = useState(0);

  useEffect(() => {
    if (message || messages.length <= 1) return;
    const id = setInterval(() => setIdx((i) => (i + 1) % messages.length), 2400);
    return () => clearInterval(id);
  }, [messages.length, message]);

  const text = message || messages[idx];

  return (
    <div className={`ls-wrap ${size === "inline" ? "ls-inline" : "ls-full"}`}>
      <div className="ls-book-scene">
        {/* Open book SVG */}
        <svg
          className="ls-book"
          viewBox="0 0 120 90"
          xmlns="http://www.w3.org/2000/svg"
          aria-hidden="true"
        >
          {/* left page */}
          <path
            className="ls-page ls-page-left"
            d="M60 12 C48 8, 10 6, 8 10 L8 78 C10 74, 48 76, 60 80 Z"
          />
          {/* right page */}
          <path
            className="ls-page ls-page-right"
            d="M60 12 C72 8, 110 6, 112 10 L112 78 C110 74, 72 76, 60 80 Z"
          />
          {/* spine */}
          <line className="ls-spine" x1="60" y1="12" x2="60" y2="80" />
          {/* text lines — left page */}
          <line className="ls-line ls-line-1" x1="18" y1="26" x2="52" y2="26" />
          <line className="ls-line ls-line-2" x1="18" y1="36" x2="48" y2="36" />
          <line className="ls-line ls-line-3" x1="18" y1="46" x2="50" y2="46" />
          {/* text lines — right page */}
          <line className="ls-line ls-line-1" x1="68" y1="26" x2="102" y2="26" />
          <line className="ls-line ls-line-2" x1="68" y1="36" x2="98" y2="36" />
          <line className="ls-line ls-line-3" x1="68" y1="46" x2="100" y2="46" />
        </svg>
      </div>

      <div className="ls-dots">
        <span className="ls-dot" />
        <span className="ls-dot" />
        <span className="ls-dot" />
      </div>

      <p className="ls-message">{text}</p>
    </div>
  );
}
