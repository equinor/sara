import { ErrorText } from "../../components/Styles";
import {
  deleteWorkflow,
  getWorkflows,
  retryWorkflow,
  type Workflow,
  type WorkflowParams,
} from "../../api/client";
import { useResourceMutation } from "../../api/queries";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import WorkflowsFilters from "./workflows-components/WorkflowsFilters";
import WorkflowsTable from "./workflows-components/WorkflowsTable";

const FILTER_KEYS: (keyof WorkflowParams & string)[] = [
  "workflowType",
  "status",
  "analysisRunId",
];

export default function WorkflowsPage() {
  const retryMutation = useResourceMutation(retryWorkflow);
  const deleteMutation = useResourceMutation(deleteWorkflow, "delete");
  const busy = retryMutation.isPending || deleteMutation.isPending;
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
  } = usePagedList<Workflow, WorkflowParams>(
    "workflows",
    "workflows.pageSize",
    FILTER_KEYS,
    getWorkflows
  );

  const items = response?.items ?? [];

  const handleRetry = async (id: string) => {
    if (busy) return;
    if (!window.confirm("Retry this workflow?")) return;
    try {
      await retryMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Retry failed");
    }
  };

  const handleDelete = async (id: string) => {
    if (busy) return;
    if (!window.confirm("Delete this workflow?")) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Workflows" loading={loading} onRefresh={refetch}>
      <WorkflowsFilters filters={filters} setFilters={setFilters} />

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
      )}

      <WorkflowsTable
        items={items}
        initialLoading={initialLoading}
        pageSize={pageSize}
        busy={busy}
        onRetry={handleRetry}
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
