import { useEffect } from "react";
import { useSearchParams } from "react-router";
import { isValidDashboardWindowHours } from "../api/client";

function readStoredWindow(storageKey: string): number | null {
  try {
    const stored = Number(localStorage.getItem(storageKey));
    return isValidDashboardWindowHours(stored) ? stored : null;
  } catch {
    return null;
  }
}

/**
 * Dashboard window (hours). A valid `?window=` wins, then the last selection in
 * localStorage, then `defaultHours`. The URL is kept in sync with the window shown,
 * so tab links without `?window=` keep the user's choice.
 */
export function useDashboardWindow(storageKey: string, defaultHours: number) {
  const [searchParams, setSearchParams] = useSearchParams();
  const fromUrl = Number(searchParams.get("window"));
  const windowHours = isValidDashboardWindowHours(fromUrl)
    ? fromUrl
    : readStoredWindow(storageKey) ?? defaultHours;

  useEffect(() => {
    if (searchParams.get("window") === String(windowHours)) return;
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set("window", String(windowHours));
        return next;
      },
      { replace: true }
    );
  }, [searchParams, windowHours, setSearchParams]);

  const setWindowHours = (hours: number) => {
    try {
      localStorage.setItem(storageKey, String(hours));
    } catch {
      /* ignore */
    }
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set("window", String(hours));
        return next;
      },
      { replace: true }
    );
  };

  return [windowHours, setWindowHours] as const;
}
