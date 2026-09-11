import { useNavigate } from "react-router";
import { Button, Table } from "@equinor/eds-core-react";
import { ErrorText, FilterBar, FilterSearch, FilterSelect, TableScroller } from "../../components/Styles";
import {
  deleteAnalysisGroup,
  getAnalysisGroups,
  type AnalysisGroup,
  type AnalysisGroupParams,
  type AnalysisGroupStatus,
} from "../../api/client";
import { useResourceMutation } from "../../api/queries";
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
  const deleteMutation = useResourceMutation(deleteAnalysisGroup, "delete");
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
  } = usePagedList<AnalysisGroup, AnalysisGroupParams>(
    "analysis-groups",
    "analysisGroups.pageSize",
    FILTER_KEYS,
    getAnalysisGroups
  );

  const items = response?.items ?? [];

  const handleDelete = async (id: string) => {
    if (deleteMutation.isPending) return;
    if (!window.confirm("Delete this analysis group? Linked records will be unlinked.")) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analysis Groups" loading={loading} onRefresh={refetch}>
      <FilterBar>
        <FilterSearch
          id="analysis-groups-group-id"
          label="Group ID"
          placeholder="Group ID"
          value={filters.groupId ?? ""}
          onChange={(e) => setFilters({ groupId: (e.target as HTMLInputElement).value })}
        />
        <FilterSelect
          id="analysis-groups-status"
          label="Status"
          value={filters.status ?? ""}
          onChange={(e) =>
            setFilters({ status: (e.target.value || undefined) as AnalysisGroupStatus | undefined })
          }
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </FilterSelect>
      </FilterBar>

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
      )}

      <TableScroller>
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
            {initialLoading ? (
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
                      disabled={deleteMutation.isPending}
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
      </TableScroller>

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
