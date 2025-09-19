import { useState, useEffect } from 'react';

export function useLocalStorage<T>(key: string, initialValue: T): [T, (value: T) => void] {
  // Get from local storage then parse stored json or return initialValue
  const [storedValue, setStoredValue] = useState<T>(() => {
    if (typeof window === "undefined") {
      return initialValue;
    }
    try {
      const item = window.localStorage.getItem(key);
      return item ? JSON.parse(item) : initialValue;
    } catch (error) {
      console.error(`Error reading localStorage key "${key}":`, error);
      return initialValue;
    }
  });

  // Return a wrapped version of useState's setter function that persists the new value to localStorage
  const setValue = (value: T) => {
    try {
      // Allow value to be a function so we have the same API as useState
      const valueToStore = value instanceof Function ? value(storedValue) : value;
      setStoredValue(valueToStore);
      
      if (typeof window !== "undefined") {
        window.localStorage.setItem(key, JSON.stringify(valueToStore));
        
        // Dispatch custom event to notify other components
        window.dispatchEvent(new CustomEvent(`localStorage-${key}`, {
          detail: valueToStore
        }));
      }
    } catch (error) {
      console.error(`Error setting localStorage key "${key}":`, error);
    }
  };

  // Listen for storage changes from other tabs/windows
  useEffect(() => {
    if (typeof window === "undefined") return;
    
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === key && e.newValue !== null) {
        try {
          setStoredValue(JSON.parse(e.newValue));
        } catch (error) {
          console.error(`Error parsing localStorage value for key "${key}":`, error);
        }
      }
    };

    // Listen for custom events from same tab
    const handleCustomEvent = (e: CustomEvent) => {
      setStoredValue(e.detail);
    };

    window.addEventListener('storage', handleStorageChange);
    window.addEventListener(`localStorage-${key}`, handleCustomEvent as EventListener);

    return () => {
      window.removeEventListener('storage', handleStorageChange);
      window.removeEventListener(`localStorage-${key}`, handleCustomEvent as EventListener);
    };
  }, [key]);

  return [storedValue, setValue];
}

// Hook specifically for watching localStorage changes without setting
export function useLocalStorageWatch(key: string): string | null {
  const [value, setValue] = useState<string | null>(() => {
    if (typeof window === "undefined") return null;
    return localStorage.getItem(key);
  });

  useEffect(() => {
    if (typeof window === "undefined") return;

    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === key) {
        setValue(e.newValue);
      }
    };

    const handleCustomEvent = (e: CustomEvent) => {
      setValue(e.detail);
    };

    // Initial value
    setValue(localStorage.getItem(key));

    window.addEventListener('storage', handleStorageChange);
    window.addEventListener(`localStorage-${key}`, handleCustomEvent as EventListener);

    return () => {
      window.removeEventListener('storage', handleStorageChange);
      window.removeEventListener(`localStorage-${key}`, handleCustomEvent as EventListener);
    };
  }, [key]);

  return value;
}