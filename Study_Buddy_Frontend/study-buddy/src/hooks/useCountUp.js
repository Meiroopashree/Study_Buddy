import { useEffect, useRef, useState } from "react";

function reducedMotion() {
  return (
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
}

export default function useCountUp(target, { duration = 900, decimals = 0 } = {}) {
  const [value, setValue] = useState(0);
  const fromRef = useRef(0);
  const startRef = useRef(0);
  const rafRef = useRef(0);

  useEffect(() => {
    if (reducedMotion()) {
      setValue(target);
      fromRef.current = target;
      return;
    }

    const from = fromRef.current;
    const to = target;
    startRef.current = performance.now();

    const tick = (now) => {
      const t = Math.min(1, (now - startRef.current) / duration);
      const eased = 1 - Math.pow(1 - t, 3);
      setValue(from + (to - from) * eased);
      if (t < 1) {
        rafRef.current = requestAnimationFrame(tick);
      } else {
        fromRef.current = to;
      }
    };

    if (from !== to) {
      rafRef.current = requestAnimationFrame(tick);
    } else {
      setValue(to);
    }

    return () => cancelAnimationFrame(rafRef.current);
  }, [target, duration]);

  const factor = Math.pow(10, decimals);
  return Math.round(value * factor) / factor;
}