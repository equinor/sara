import { useNavigate } from "react-router";
import { Button, Search, Table, Typography } from "@equinor/eds-core-react";
import {
  deleteWorkflow,
  getWorkflows,
  retryWorkflow,
  type Workflow,
  type WorkflowParams,
  type WorkflowStatus,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";

const FILTER_KEYS: (keyof WorkflowParams & string)[] = [
  "workflowType",
  "status",
  "analysisRunId",
];
const STATUSES: WorkflowStatus[] = ["Pending", "InProgress", "Succeeded", "Failed"];

export default function WorkflowsPage() {
  const navigate = useNavigate();
  const {
    response,
    loading,
    error,
    pageNumber,
    pageSize,
    filters,
    setPage,
    setPageSize,
    setFilters,
    refetch,
  } = usePagedList<Workflow, WorkflowParams>(
    "workflows.pageSize",
    FILTER_KEYS,
    getWorkflows
  );

  const items = response?.items ?? [];
  const showSkeleton = loading || (response === null && error === null);

  const handleRetry = async (id: string) => {
    if (!window.confirm("Retry this workflow?")) return;
    try {
      await retryWorkflow(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Retry failed");
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("Delete this workflow?")) return;
    try {
      await deleteWorkflow(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Workflows" loading={loading} onRefresh={refetch}>
      <div style={{ display: "flex", gap: "0.75rem", marginBottom: "1rem", flexWrap: "wrap" }}>
        <Search
          placeholder="Workflow Type"
          value={filters.workflowType ?? ""}
          onChange={(e) => setFilters({ workflowType: (e.target as HTMLInputElement).value })}
        />
        <Search
          placeholder="Analysis Run ID (uuid)"
          value={filters.analysisRunId ?? ""}
          onChange={(e) => setFilters({ analysisRunId: (e.target as HTMLInputElement).value })}
        />
        <select
          value={filters.status ?? ""}
          onChange={(e) =>
            setFilters({ status: (e.target.value || undefined) as WorkflowStatus | undefined })
          }
          style={{ padding: "0.4rem" }}
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <Typography variant="body_short" style={{ color: "#eb0000", marginBottom: "1rem" }}>
          {error}
        </Typography>
      )}

      <Table style={{ width: "100%" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Step</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>Run</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {showSkeleton ? (
            <TableSkeleton columns={8} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={8}>No workflows.</Table.Cell>
            </Table.Row>
          ) : (
            items.map((w) => (
              <Table.Row
                key={w.id}
                onClick={() => navigate(`/workflows/${w.id}`)}
                style={{ cursor: "pointer" }}
              >
                <Table.Cell>
                  <IdCell id={w.id} />
                </Table.Cell>
                <Table.Cell>{w.workflowType}</Table.Cell>
                <Table.Cell>{w.stepNumber}</Table.Cell>
                <Table.Cell>
                  <StatusChip status={w.status} />
                </Table.Cell>
                <Table.Cell>
                  {w.startedAt ? new Date(w.startedAt).toLocaleString() : "–"}
                </Table.Cell>
                <Table.Cell>
                  {w.completedAt ? new Date(w.completedAt).toLocaleString() : "–"}
                </Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    onClick={(e) => {
                      e.stopPropagation();
                      navigate(`/analysis-runs/${w.analysisRunId}`);
                    }}
                  >
                    View
                  </Button>
                </Table.Cell>
                <Table.Cell>
                  {w.status === "Failed" && (
                    <Button
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRetry(w.id);
                      }}
                    >
                      Retry
                    </Button>
                  )}
                  <Button
                    variant="ghost"
                    color="danger"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleDelete(w.id);
                    }}
                  >
                    Delete
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>

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
