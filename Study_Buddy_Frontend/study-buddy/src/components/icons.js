import React from "react";

function base(props) {
  const { size = 20, width, height, ...rest } = props;
  return {
    xmlns: "http://www.w3.org/2000/svg",
    viewBox: "0 0 24 24",
    width,
    height,
    ...(width == null && height == null ? { width: size, height: size } : {}),
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.8,
    strokeLinecap: "round",
    strokeLinejoin: "round",
    "aria-hidden": true,
    ...rest,
  };
}

const S = {
  chat: (p) => (
    <svg {...base(p)}>
      <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
    </svg>
  ),
  dashboard: (p) => (
    <svg {...base(p)}>
      <rect x="3" y="3" width="7" height="9" rx="1.5" />
      <rect x="14" y="3" width="7" height="5" rx="1.5" />
      <rect x="14" y="12" width="7" height="9" rx="1.5" />
      <rect x="3" y="16" width="7" height="5" rx="1.5" />
    </svg>
  ),
  book: (p) => (
    <svg {...base(p)}>
      <path d="M12 6.5C10.5 5 8 4.5 4 5v13c4-.5 6.5 0 8 1.5 1.5-1.5 4-2 8-1.5V5c-4-.5-6.5 0-8 1.5z" />
      <path d="M12 6.5v13" />
    </svg>
  ),
  paper: (p) => (
    <svg {...base(p)}>
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <path d="M14 2v6h6" />
      <path d="M9 13h6M9 17h4" />
    </svg>
  ),
  chart: (p) => (
    <svg {...base(p)}>
      <path d="M3 3v18h18" />
      <path d="M7 14l3-4 3 3 5-6" />
    </svg>
  ),
  user: (p) => (
    <svg {...base(p)}>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c0-4 3.6-6 8-6s8 2 8 6" />
    </svg>
  ),
  sun: (p) => (
    <svg {...base(p)}>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </svg>
  ),
  moon: (p) => (
    <svg {...base(p)}>
      <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
    </svg>
  ),
  logout: (p) => (
    <svg {...base(p)}>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="M16 17l5-5-5-5M21 12H9" />
    </svg>
  ),
  target: (p) => (
    <svg {...base(p)}>
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="5" />
      <circle cx="12" cy="12" r="1" />
    </svg>
  ),
  clipboard: (p) => (
    <svg {...base(p)}>
      <rect x="5" y="4" width="14" height="17" rx="2" />
      <path d="M9 4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2" />
      <path d="M9 11h6M9 15h6" />
    </svg>
  ),
  clock: (p) => (
    <svg {...base(p)}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  ),
  calendar: (p) => (
    <svg {...base(p)}>
      <rect x="3" y="4" width="18" height="17" rx="2" />
      <path d="M3 9h18M8 2v4M16 2v4" />
    </svg>
  ),
  notebook: (p) => (
    <svg {...base(p)}>
      <path d="M6 2h12a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H9a4 4 0 0 1-4-4V3a1 1 0 0 1 1-1z" />
      <path d="M5 17.5A3.5 3.5 0 0 1 8.5 14H19" />
      <path d="M9 7h7M9 11h5" />
    </svg>
  ),
  doc: (p) => (
    <svg {...base(p)}>
      <path d="M6 2h9l5 5v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z" />
      <path d="M14 2v6h6" />
      <path d="M9 13h6" />
    </svg>
  ),
  brain: (p) => (
    <svg {...base(p)}>
      <path d="M9.5 3a2.5 2.5 0 0 0-2.45 3A2.5 2.5 0 0 0 5 8.5 2.5 2.5 0 0 0 6.5 13 2.5 2.5 0 0 0 9 15.5V21a1 1 0 0 0 1 1 1 1 0 0 0 1-1v-1h2v1a1 1 0 0 0 1 1 1 1 0 0 0 1-1v-5.5A2.5 2.5 0 0 0 17.5 13a2.5 2.5 0 0 0 1.5-4.5A2.5 2.5 0 0 0 16.95 6 2.5 2.5 0 0 0 14.5 3a2.5 2.5 0 0 0-5 0z" />
    </svg>
  ),
  trending: (p) => (
    <svg {...base(p)}>
      <path d="M3 17l6-6 4 4 8-8" />
      <path d="M15 7h6v6" />
    </svg>
  ),
  bookmark: (p) => (
    <svg {...base(p)}>
      <path d="M6 3h12a1 1 0 0 1 1 1v17l-7-4-7 4V4a1 1 0 0 1 1-1z" />
    </svg>
  ),
  play: (p) => (
    <svg {...base(p)} fill="currentColor" stroke="none">
      <path d="M8 5.5v13l11-6.5z" />
    </svg>
  ),
  send: (p) => (
    <svg {...base(p)}>
      <path d="M22 2L11 13" />
      <path d="M22 2l-7 20-4-9-9-4z" />
    </svg>
  ),
  mic: (p) => (
    <svg {...base(p)}>
      <rect x="9" y="3" width="6" height="11" rx="3" />
      <path d="M5 11a7 7 0 0 0 14 0M12 18v3" />
    </svg>
  ),
  upload: (p) => (
    <svg {...base(p)}>
      <path d="M12 16V4M7 9l5-5 5 5" />
      <path d="M4 20h16" />
    </svg>
  ),
  image: (p) => (
    <svg {...base(p)}>
      <rect x="3" y="3" width="18" height="18" rx="2" />
      <circle cx="8.5" cy="8.5" r="1.5" />
      <path d="M21 15l-5-5L5 21" />
    </svg>
  ),
  sparkle: (p) => (
    <svg {...base(p)}>
      <path d="M12 3l1.6 4.6L18 9.2l-4.4 1.6L12 15.4l-1.6-4.6L6 9.2l4.4-1.6z" />
      <path d="M18.5 14.5l.7 2 2 .7-2 .7-.7 2-.7-2-2-.7 2-.7z" />
    </svg>
  ),
  check: (p) => (
    <svg {...base(p)}>
      <path d="M5 12l5 5L20 7" />
    </svg>
  ),
  download: (p) => (
    <svg {...base(p)}>
      <path d="M12 3v12M7 10l5 5 5-5" />
      <path d="M4 21h16" />
    </svg>
  ),
  edit: (p) => (
    <svg {...base(p)}>
      <path d="M12 20h9" />
      <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z" />
    </svg>
  ),
  trash: (p) => (
    <svg {...base(p)}>
      <path d="M4 6h16M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6M10 11v6M14 11v6" />
    </svg>
  ),
  x: (p) => (
    <svg {...base(p)}>
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  ),
  menu: (p) => (
    <svg {...base(p)}>
      <path d="M4 6h16M4 12h16M4 18h16" />
    </svg>
  ),
  search: (p) => (
    <svg {...base(p)}>
      <circle cx="11" cy="11" r="7" />
      <path d="M20 20l-4-4" />
    </svg>
  ),
  chevron: (p) => (
    <svg {...base(p)}>
      <path d="M6 9l6 6 6-6" />
    </svg>
  ),
  star: (p) => (
    <svg {...base(p)} fill="currentColor" stroke="none">
      <path d="M12 2l2.9 6.2 6.6.9-4.8 4.6 1.2 6.6L12 17.2l-5.9 3.1 1.2-6.6L2.5 9.1l6.6-.9z" />
    </svg>
  ),
  notes: (p) => (
    <svg {...base(p)}>
      <path d="M6 2h12a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H9a4 4 0 0 1-4-4V3a1 1 0 0 1 1-1z" />
      <path d="M9 7h7M9 11h5" />
    </svg>
  ),
  bookmarkFill: (p) => (
    <svg {...base(p)} fill="currentColor" stroke="none">
      <path d="M6 3h12a1 1 0 0 1 1 1v17l-7-4-7 4V4a1 1 0 0 1 1-1z" />
    </svg>
  ),
  plus: (p) => (
    <svg {...base(p)}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  ),
  expand: (p) => (
    <svg {...base(p)}>
      <path d="M7 9l5-5 5 5M7 15l5 5 5-5" />
    </svg>
  ),
  collapse: (p) => (
    <svg {...base(p)}>
      <path d="M7 9l5 5 5-5M7 15l5-5 5 5" />
    </svg>
  ),
  fullscreen: (p) => (
    <svg {...base(p)}>
      <path d="M8 3H5a2 2 0 0 0-2 2v3M16 3h3a2 2 0 0 1 2 2v3M8 21H5a2 2 0 0 1-2-2v-3M16 21h3a2 2 0 0 0 2-2v-3" />
    </svg>
  ),
  rotate: (p) => (
    <svg {...base(p)}>
      <path d="M21 12a9 9 0 1 1-3-6.7L21 8" />
      <path d="M21 3v5h-5" />
    </svg>
  ),
  alert: (p) => (
    <svg {...base(p)}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 8v4M12 16h.01" />
    </svg>
  ),
  compare: (p) => (
    <svg {...base(p)}>
      <rect x="4" y="3" width="7" height="18" rx="1.5" />
      <rect x="13" y="3" width="7" height="18" rx="1.5" />
    </svg>
  ),
  share: (p) => (
    <svg {...base(p)}>
      <circle cx="18" cy="5" r="3" />
      <circle cx="6" cy="12" r="3" />
      <circle cx="18" cy="19" r="3" />
      <path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4" />
    </svg>
  ),
  lock: (p) => (
    <svg {...base(p)}>
      <rect x="5" y="11" width="14" height="10" rx="2" />
      <path d="M8 11V7a4 4 0 0 1 8 0v4" />
    </svg>
  ),
  google: (p) => {
    const { size = 20 } = p;
    return (
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 24 24"
        width={size}
        height={size}
        aria-hidden="true"
      >
        <path fill="#4285F4" d="M23.5 12.27c0-.79-.07-1.54-.19-2.27H12v4.51h6.47c-.29 1.48-1.14 2.73-2.4 3.58v3h3.86c2.27-2.09 3.56-5.17 3.56-8.82z" />
        <path fill="#34A853" d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.86-3c-1.08.72-2.45 1.16-4.07 1.16-3.13 0-5.78-2.11-6.73-4.96H1.29v3.09C3.26 21.3 7.31 24 12 24z" />
        <path fill="#FBBC05" d="M5.27 14.29c-.25-.72-.38-1.49-.38-2.29s.14-1.57.38-2.29V6.62H1.29C.47 8.24 0 10.06 0 12s.47 3.76 1.29 5.38l3.98-3.09z" />
        <path fill="#EA4335" d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.31 0 3.26 2.7 1.29 6.62l3.98 3.09c.95-2.85 3.6-4.96 6.73-4.96z" />
      </svg>
    );
  },
};

export const Icon = { ...S };
export default S;