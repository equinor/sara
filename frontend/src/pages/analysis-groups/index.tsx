import { useNavigate } from "react-router";
import { Button, Search, Table, Typography } from "@equinor/eds-core-react";
import {
  deleteAnalysisGroup,
  getAnalysisGroups,
  type AnalysisGroup,
  type AnalysisGroupParams,
  type AnalysisGroupStatus,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";

const FILTER_KEYS: (keyof AnalysisGroupParams & string)[] = ["groupId", "status"];
const STATUSES: AnalysisGroupStatus[] = ["Pending", "Complete", "TimedOut"];

export default function AnalysisGroupsPage() {
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
  } = usePagedList<AnalysisGroup, AnalysisGroupParams>(
    "analysisGroups.pageSize",
    FILTER_KEYS,
    getAnalysisGroups
  );

  const items = response?.items ?? [];
  const showSkeleton = loading || (response === null && error === null);

  const handleDelete = async (id: string) => {
    if (!window.confirm("Delete this analysis group? Linked records will be unlinked.")) return;
    try {
      await deleteAnalysisGroup(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analysis Groups" loading={loading} onRefresh={refetch}>
      <div style={{ display: "flex", gap: "0.75rem", marginBottom: "1rem", flexWrap: "wrap" }}>
        <Search
          placeholder="Group ID"
          value={filters.groupId ?? ""}
          onChange={(e) => setFilters({ groupId: (e.target as HTMLInputElement).value })}
        />
        <select
          value={filters.status ?? ""}
          onChange={(e) =>
            setFilters({ status: (e.target.value || undefined) as AnalysisGroupStatus | undefined })
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
            <Table.Cell>Group ID</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Expected</Table.Cell>
            <Table.Cell>#Records</Table.Cell>
            <Table.Cell>#Analyses</Table.Cell>
            <Table.Cell>Timeout</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {showSkeleton ? (
            <TableSkeleton columns={8} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={8}>No analysis groups.</Table.Cell>
            </Table.Row>
          ) : (
            items.map((g) => (
              <Table.Row
                key={g.id}
                onClick={() => navigate(`/analysis-groups/${g.id}`)}
                style={{ cursor: "pointer" }}
              >
                <Table.Cell>
                  <IdCell id={g.id} />
                </Table.Cell>
                <Table.Cell>{g.groupId}</Table.Cell>
                <Table.Cell>
                  <StatusChip status={g.status} />
                </Table.Cell>
                <Table.Cell>{g.expectedSize}</Table.Cell>
                <Table.Cell>{(g.inspectionRecords ?? []).length}</Table.Cell>
                <Table.Cell>{(g.analyses ?? []).length}</Table.Cell>
                <Table.Cell>
                  {g.timeoutAt ? new Date(g.timeoutAt).toLocaleString() : "–"}
                </Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    color="danger"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleDelete(g.id);
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
