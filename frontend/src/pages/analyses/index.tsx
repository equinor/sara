import { ErrorText } from "../../components/Styles";
import {
  deleteAnalysis,
  getAnalyses,
  rerunAnalysis,
  type Analysis,
  type AnalysisParams,
} from "../../api/client";
import { useResourceMutation } from "../../api/queries";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import AnalysesFilters from "./analyses-components/AnalysesFilters";
import AnalysesTable from "./analyses-components/AnalysesTable";

const FILTER_KEYS: (keyof AnalysisParams & string)[] = [
  "name",
  "analysisGroupId",
  "inspectionRecordId",
];

export default function AnalysesPage() {
  const rerunMutation = useResourceMutation(rerunAnalysis);
  const deleteMutation = useResourceMutation(deleteAnalysis, "delete");
  const busy = rerunMutation.isPending || deleteMutation.isPending;
  const {
    response,
    loading,
    initialLoading,
    error,
    pageNumber,
    pageSize,
    filters,
    setPage,
    setPageSize,
    setFilters,
    refetch,
  } = usePagedList<Analysis, AnalysisParams>(
    "analyses",
    "analyses.pageSize",
    FILTER_KEYS,
    getAnalyses
  );

  const items = response?.items ?? [];

  const handleRerun = async (id: string) => {
    if (busy) return;
    if (!window.confirm("Trigger a new run of this analysis?")) return;
    try {
      await rerunMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Rerun failed");
    }
  };

  const handleDelete = async (id: string) => {
    if (busy) return;
    if (!window.confirm("Delete this analysis and its runs?")) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analyses" loading={loading} onRefresh={refetch}>
      <AnalysesFilters filters={filters} setFilters={setFilters} />

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
      )}

      <AnalysesTable
        items={items}
        initialLoading={initialLoading}
        pageSize={pageSize}
        busy={busy}
        onRerun={handleRerun}
        onDelete={handleDelete}
      />

      <PaginationFooter
        hasResponse={response !== null}
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={response?.totalCount ?? null}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        disabled={loading}
        loading={loading}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        resetKey={`${pageSize}-${pageNumber}`}
      />
    </PageHeader>
  );
}
