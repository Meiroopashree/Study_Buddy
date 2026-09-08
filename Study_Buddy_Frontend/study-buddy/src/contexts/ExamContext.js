import React, { createContext, useContext, useState, useCallback } from "react";

const ExamContext = createContext(null);

export function ExamProvider({ children }) {
  const [currentExam, setCurrentExamRaw] = useState(
    () => localStorage.getItem("sb-current-exam") || ""
  );

  const setCurrentExam = useCallback((exam) => {
    setCurrentExamRaw(exam || "");
    if (exam) localStorage.setItem("sb-current-exam", exam);
    else localStorage.removeItem("sb-current-exam");
  }, []);

  return (
    <ExamContext.Provider value={{ currentExam, setCurrentExam }}>
      {children}
    </ExamContext.Provider>
  );
}

export function useExam() {
  return useContext(ExamContext);
}