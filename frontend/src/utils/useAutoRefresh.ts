import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Fetches data on mount and re-fetches on a fixed interval. Polling pauses
 * while the tab is hidden (Page Visibility API) and resumes (with an immediate
 * refresh) when it becomes visible again.
 */
export function useAutoRefresh<T>(
  fetcher: () => Promise<T>,
  intervalMs = 60000,
  deps: unknown[] = []
) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  // Keep the latest fetcher without retriggering the polling effect.
  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  const refetch = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await fetcherRef.current();
      setData(result);
      setLastUpdated(new Date());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to fetch");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    const tick = () => {
      if (!cancelled && !document.hidden) refetch();
    };

    refetch();
    const id = window.setInterval(tick, intervalMs);

    const onVisibility = () => {
      if (!document.hidden) refetch();
    };
    document.addEventListener("visibilitychange", onVisibility);

    return () => {
      cancelled = true;
      window.clearInterval(id);
      document.removeEventListener("visibilitychange", onVisibility);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [refetch, intervalMs, ...deps]);

  return { data, loading, error, lastUpdated, refetch };
}
