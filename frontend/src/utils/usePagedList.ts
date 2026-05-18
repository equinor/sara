import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router";
import type { PagedResponse } from "../api/client";

export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];
const DEFAULT_PAGE_SIZE = 25;

function readStoredPageSize(key: string): number {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return DEFAULT_PAGE_SIZE;
    const n = Number(raw);
    return PAGE_SIZE_OPTIONS.includes(n) ? n : DEFAULT_PAGE_SIZE;
  } catch {
    return DEFAULT_PAGE_SIZE;
  }
}

/**
 * URL-backed pagination + filter state, paired with a data fetcher.
 * `filterKeys` are the URL query params that count as filters.
 */
export function usePagedList<T, F extends object>(
  storageKey: string,
  filterKeys: (keyof F & string)[],
  fetcher: (
    pageNumber: number,
    pageSize: number,
    filters: F
  ) => Promise<PagedResponse<T>>
) {
  const [searchParams, setSearchParams] = useSearchParams();

  const pageNumber = useMemo(() => {
    const n = Number(searchParams.get("page"));
    return Number.isFinite(n) && n >= 1 ? Math.floor(n) : 1;
  }, [searchParams]);

  const pageSize = useMemo(() => {
    const fromUrl = Number(searchParams.get("pageSize"));
    if (PAGE_SIZE_OPTIONS.includes(fromUrl)) return fromUrl;
    return readStoredPageSize(storageKey);
  }, [searchParams, storageKey]);

  const filters = useMemo<F>(() => {
    const obj = {} as Record<string, string | undefined>;
    for (const key of filterKeys) {
      obj[key] = searchParams.get(key) ?? undefined;
    }
    return obj as F;
  }, [searchParams, filterKeys]);

  const [response, setResponse] = useState<PagedResponse<T> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setResponse(await fetcher(pageNumber, pageSize, filters));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to fetch");
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, filters, fetcher]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (searchParams.get("pageSize") !== String(pageSize)) {
      const next = new URLSearchParams(searchParams);
      next.set("pageSize", String(pageSize));
      if (!next.get("page")) next.set("page", "1");
      setSearchParams(next, { replace: true });
    }
  }, [pageSize, searchParams, setSearchParams]);

  const updateParams = useCallback(
    (updates: Record<string, string | number | null>) => {
      const next = new URLSearchParams(searchParams);
      for (const [key, value] of Object.entries(updates)) {
        if (value === null) next.delete(key);
        else next.set(key, String(value));
      }
      setSearchParams(next);
      window.scrollTo({ top: 0, behavior: "smooth" });
    },
    [searchParams, setSearchParams]
  );

  const setPage = useCallback(
    (p: number) => {
      if (p !== pageNumber) updateParams({ page: p });
    },
    [pageNumber, updateParams]
  );

  const setPageSize = useCallback(
    (s: number) => {
      try {
        localStorage.setItem(storageKey, String(s));
      } catch { /* ignore */ }
      updateParams({ pageSize: s, page: 1 });
    },
    [storageKey, updateParams]
  );

  const setFilters = useCallback(
    (next: Partial<F>) => {
      const url = new URLSearchParams(searchParams);
      for (const key of filterKeys) {
        const v = next[key];
        if (v == null || v === "") url.delete(key);
        else url.set(key, String(v));
      }
      url.set("page", "1");
      setSearchParams(url);
    },
    [searchParams, setSearchParams, filterKeys]
  );

  return {
    response,
    loading,
    error,
    pageNumber,
    pageSize,
    filters,
    setPage,
    setPageSize,
    setFilters,
    refetch: fetchData,
  };
}
