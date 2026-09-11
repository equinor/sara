import {
  deleteAnalysisRun,
  getAnalysisRuns,
  type AnalysisRun,
  type AnalysisRunParams,
} from "../../api/client";
import { useConfiguredAnalyses, useResourceMutation } from "../../api/queries";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import { ErrorText } from "../../components/Styles";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import AnalysisRunsFilters from "./analysis-runs-components/AnalysisRunsFilters";
import AnalysisRunsTable from "./analysis-runs-components/AnalysisRunsTable";

const FILTER_KEYS: (keyof AnalysisRunParams & string)[] = [
  "analysisId",
  "analysisType",
  "status",
  "startedSince",
  "startedUntil",
];

export default function AnalysisRunsPage() {
  const deleteMutation = useResourceMutation(deleteAnalysisRun, "delete");
  const configuredAnalyses = useConfiguredAnalyses();
  const analysisTypes = (configuredAnalyses.data ?? [])
    .map((analysis) => analysis.name)
    .sort((a, b) => a.localeCompare(b));
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
  } = usePagedList<AnalysisRun, AnalysisRunParams>(
    "analysis-runs",
    "analysisRuns.pageSize",
    FILTER_KEYS,
    getAnalysisRuns
  );

  const items = response?.items ?? [];

  const handleDelete = async (id: string) => {
    if (deleteMutation.isPending) return;
    if (!window.confirm("Delete this run and its workflows?")) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analysis Runs" loading={loading} onRefresh={refetch}>
      <AnalysisRunsFilters
        filters={filters}
        setFilters={setFilters}
        analysisTypes={analysisTypes}
        configuredAnalysesError={configuredAnalyses.error}
      />

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
      )}

      <AnalysisRunsTable
        items={items}
        initialLoading={initialLoading}
        pageSize={pageSize}
        busy={deleteMutation.isPending}
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
