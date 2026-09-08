function deindentLines(lines) {
  const nonEmpty = lines.filter((l) => l.trim().length > 0);
  const indents = nonEmpty.map((l) => (l.match(/^\s*/) || [""])[0].length);
  const min = Math.min(...indents);
  return nonEmpty.map((l) => l.slice(min).trimEnd());
}

// Build a column-0 $$ block from raw body lines. Wraps multi-line step math in
// \begin{aligned} only when no \begin/\end environment is present (wrapping a
// matrix or cases environment that way makes KaTeX raise a mismatch error).
function wrapDisplay(bodyLines) {
  const lines = deindentLines(bodyLines);
  const content = lines.join("\n").trim();
  if (
    lines.length > 1 &&
    !content.includes("\\begin{") &&
    !content.includes("\\end{")
  ) {
    return ["$$", `\\begin{aligned}`, ...lines, `\\end{aligned}`, "$$"];
  }
  return ["$$", ...lines, "$$"];
}

function formatAIText(text) {
  if (!text) return "";

  // Inline math \(...\) → $...$.
  let s = text.replace(/\\\(([\s\S]*?)\\\)/g, (_m, body) => `$${body.trim()}$`);

  const lines = s.split("\n");
  const out = [];
  let inDisplay = false;
  let i = 0;
  while (i < lines.length) {
    const raw = lines[i];
    const t = raw.trim();

    if (t === "$$") {
      inDisplay = !inDisplay;
      out.push("$$");
      i++;
      continue;
    }

    if (inDisplay) {
      if (t) out.push(t);
      i++;
      continue;
    }

    // $$ glued to trailing text (e.g. "$$)." after a converted \[...\] block).
    if (t.startsWith("$$")) {
      let rest = t.slice(2).trim();
      out.push("$$");
      inDisplay = true;
      if (rest.endsWith("$$")) {
        const inner = rest.slice(0, -2).trim();
        if (inner) out.push(inner);
        out.push("$$");
        inDisplay = false;
      } else if (rest) {
        out.push(rest);
      }
      i++;
      continue;
    }

    // Display \[...\] block, possibly spanning lines, possibly inline.
    const openIdx = t.indexOf("\\[");
    if (openIdx >= 0) {
      const pre = t.slice(0, openIdx).trim();
      if (pre) out.push(pre);
      const body = [];
      let j = i;
      let closed = false;
      while (j < lines.length) {
        const cur = j === i ? t.slice(openIdx + 2) : lines[j];
        const closeIdx = cur.indexOf("\\]");
        if (closeIdx >= 0) {
          const before = cur.slice(0, closeIdx).trim();
          if (before) body.push(before);
          const after = cur.slice(closeIdx + 2).trim();
          out.push(...wrapDisplay(body));
          if (after) lines.splice(j + 1, 0, after);
          i = j + 1;
          closed = true;
          break;
        }
        if (j === i ? t.slice(openIdx + 2).trim() : cur.trim()) body.push(cur);
        j++;
      }
      if (!closed) {
        out.push(...wrapDisplay(body));
        i = j;
      }
      continue;
    }

    // Bare environment span \begin{...}...\end{...} (user-paste style).
    if (t.startsWith("\\begin{")) {
      const span = [];
      let depth = 0;
      let j = i;
      while (j < lines.length) {
        const l = lines[j];
        depth +=
          (l.match(/\\begin\{/g) || []).length -
          (l.match(/\\end\{/g) || []).length;
        span.push(l);
        j++;
        if (depth <= 0) break;
      }
      out.push(...wrapDisplay(span));
      i = j;
      continue;
    }

    // Lone \command line.
    if (!t.includes("$") && t.startsWith("\\") && /\\[a-zA-Z]/.test(t)) {
      out.push("$$", t, "$$");
      i++;
      continue;
    }

    out.push(raw);
    i++;
  }
  return out.join("\n");
}

export default formatAIText;
