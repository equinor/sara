import { useNavigate } from "react-router";
import { Button, Search, Table, Typography } from "@equinor/eds-core-react";
import {
  deleteAnalysisRun,
  getAnalysisRuns,
  type AnalysisRun,
  type AnalysisRunParams,
  type AnalysisRunStatus,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";

const FILTER_KEYS: (keyof AnalysisRunParams & string)[] = ["analysisId", "status"];
const STATUSES: AnalysisRunStatus[] = ["Pending", "InProgress", "Succeeded", "Failed"];

export default function AnalysisRunsPage() {
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
  } = usePagedList<AnalysisRun, AnalysisRunParams>(
    "analysisRuns.pageSize",
    FILTER_KEYS,
    getAnalysisRuns
  );

  const items = response?.items ?? [];
  const showSkeleton = loading || (response === null && error === null);

  const handleDelete = async (id: string) => {
    if (!window.confirm("Delete this run and its workflows?")) return;
    try {
      await deleteAnalysisRun(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analysis Runs" loading={loading} onRefresh={refetch}>
      <div style={{ display: "flex", gap: "0.75rem", marginBottom: "1rem", flexWrap: "wrap" }}>
        <Search
          placeholder="Analysis ID (uuid)"
          value={filters.analysisId ?? ""}
          onChange={(e) => setFilters({ analysisId: (e.target as HTMLInputElement).value })}
        />
        <select
          value={filters.status ?? ""}
          onChange={(e) =>
            setFilters({ status: (e.target.value || undefined) as AnalysisRunStatus | undefined })
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
            <Table.Cell>Analysis</Table.Cell>
            <Table.Cell>Run #</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>#Workflows</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {showSkeleton ? (
            <TableSkeleton columns={8} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={8}>No runs.</Table.Cell>
            </Table.Row>
          ) : (
            items.map((r) => (
              <Table.Row
                key={r.id}
                onClick={() => navigate(`/analysis-runs/${r.id}`)}
                style={{ cursor: "pointer" }}
              >
                <Table.Cell>
                  <IdCell id={r.id} />
                </Table.Cell>
                <Table.Cell>
                  {r.analysis ? (
                    <Button
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/analyses/${r.analysisId}`);
                      }}
                    >
                      {r.analysis.name}
                    </Button>
                  ) : (
                    r.analysisId
                  )}
                </Table.Cell>
                <Table.Cell>{r.runNumber}</Table.Cell>
                <Table.Cell>
                  <StatusChip status={r.status} />
                </Table.Cell>
                <Table.Cell>
                  {r.startedAt ? new Date(r.startedAt).toLocaleString() : "–"}
                </Table.Cell>
                <Table.Cell>
                  {r.completedAt ? new Date(r.completedAt).toLocaleString() : "–"}
                </Table.Cell>
                <Table.Cell>{(r.workflows ?? []).length}</Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    color="danger"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleDelete(r.id);
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
