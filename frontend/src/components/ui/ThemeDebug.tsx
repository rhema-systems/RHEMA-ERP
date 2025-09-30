"use client";

import React, { useEffect, useState } from 'react';
import { useTheme } from '../../contexts/ThemeContext';

export function ThemeDebug() {
  const { theme, actualTheme, systemTheme, toggleTheme, isSystemTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  const [htmlClasses, setHtmlClasses] = useState<string>('');

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const classes = document.documentElement.className;
      setHtmlClasses(classes);
    }
  }, [actualTheme]);

  if (!mounted) return null;

  return (
    <div className="fixed bottom-4 right-4 p-4 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg shadow-lg text-xs font-mono z-50">
      <h3 className="font-bold mb-2">Theme Debug</h3>
      <div>Theme: {theme}</div>
      <div>Actual: {actualTheme}</div>
      <div>System: {systemTheme}</div>
      <div>IsSystem: {isSystemTheme ? 'Yes' : 'No'}</div>
      <div>HTML Classes: {htmlClasses}</div>
      <button 
        onClick={toggleTheme}
        className="mt-2 px-2 py-1 bg-blue-500 text-white rounded text-xs"
      >
        Toggle Theme
      </button>
    </div>
  );
}