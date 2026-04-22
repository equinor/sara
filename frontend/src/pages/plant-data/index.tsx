import { useEffect, useState, useCallback, useMemo } from "react";
import { Button, Typography, Icon } from "@equinor/eds-core-react";
import { add, refresh } from "@equinor/eds-icons";
import { useNavigate, useSearchParams } from "react-router";
import {
  getPlantData,
  triggerAnonymizer,
  type PagedResponse,
  type PlantData,
} from "../../api/client";
import PlantDataTable from "./PlantDataTable";
import PaginationFooter from "../../components/PaginationFooter";

Icon.add({ add, refresh });

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];
const DEFAULT_PAGE_SIZE = 25;
const PAGE_SIZE_STORAGE_KEY = "plantData.pageSize";

function readStoredPageSize(): number {
  try {
    const raw = localStorage.getItem(PAGE_SIZE_STORAGE_KEY);
    if (!raw) return DEFAULT_PAGE_SIZE;
    const n = Number(raw);
    return PAGE_SIZE_OPTIONS.includes(n) ? n : DEFAULT_PAGE_SIZE;
  } catch {
    return DEFAULT_PAGE_SIZE;
  }
}

export default function PlantDataPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const pageNumber = useMemo(() => {
    const n = Number(searchParams.get("page"));
    return Number.isFinite(n) && n >= 1 ? Math.floor(n) : 1;
  }, [searchParams]);

  const pageSize = useMemo(() => {
    const fromUrl = Number(searchParams.get("pageSize"));
    if (PAGE_SIZE_OPTIONS.includes(fromUrl)) return fromUrl;
    return readStoredPageSize();
  }, [searchParams]);

  const [response, setResponse] = useState<PagedResponse<PlantData> | null>(
    null
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getPlantData(pageNumber, pageSize);
      setResponse(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to fetch plant data");
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize]);

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

  const handlePageChange = (page: number) => {
    if (page === pageNumber) return;
    updateParams({ page });
  };

  const handlePageSizeChange = (newSize: number) => {
    try {
      localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(newSize));
    } catch { }
    updateParams({ pageSize: newSize, page: 1 });
  };

  const handleTriggerAnonymizer = async (id: string) => {
    setTriggeringId(id);
    try {
      await triggerAnonymizer(id);
      await fetchData();
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Failed to trigger anonymizer"
      );
    } finally {
      setTriggeringId(null);
    }
  };

  const hasResponse = response !== null;
  const showTableSkeleton = loading || (!hasResponse && error === null);
  const items = response?.items ?? [];
  const totalCount = response?.totalCount ?? null;

  return (
    <div style={{ paddingTop: "1rem" }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "1rem",
        }}
      >
        <Typography variant="h3">Plant Data</Typography>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="ghost_icon"
            onClick={fetchData}
            aria-label="Refresh"
            disabled={loading}
          >
            <Icon name="refresh" />
          </Button>
          <Button onClick={() => navigate("/create-plant-data")}>
            <Icon name="add" />
            New Plant Data
          </Button>
        </div>
      </div>

      {error && (
        <Typography
          variant="body_short"
          color="danger"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      <PlantDataTable
        data={items}
        hasLoaded={hasResponse}
        loading={showTableSkeleton}
        pageSize={pageSize}
        triggeringId={triggeringId}
        onTriggerAnonymizer={handleTriggerAnonymizer}
      />

      <PaginationFooter
        hasResponse={hasResponse}
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        disabled={loading}
        loading={loading}
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
        resetKey={`${pageSize}-${pageNumber}`}
      />
    </div>
  );
}
