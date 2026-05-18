import { useNavigate } from "react-router";
import { Button, Search, Table, Typography } from "@equinor/eds-core-react";
import {
  deleteAnalysis,
  getAnalyses,
  rerunAnalysis,
  type Analysis,
  type AnalysisParams,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";

const FILTER_KEYS: (keyof AnalysisParams & string)[] = [
  "name",
  "analysisGroupId",
  "inspectionRecordId",
];

export default function AnalysesPage() {
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
  } = usePagedList<Analysis, AnalysisParams>(
    "analyses.pageSize",
    FILTER_KEYS,
    getAnalyses
  );

  const items = response?.items ?? [];
  const showSkeleton = loading || (response === null && error === null);

  const handleRerun = async (id: string) => {
    if (!window.confirm("Trigger a new run of this analysis?")) return;
    try {
      await rerunAnalysis(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Rerun failed");
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("Delete this analysis and its runs?")) return;
    try {
      await deleteAnalysis(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analyses" loading={loading} onRefresh={refetch}>
      <div style={{ display: "flex", gap: "0.75rem", marginBottom: "1rem", flexWrap: "wrap" }}>
        <Search
          placeholder="Name"
          value={filters.name ?? ""}
          onChange={(e) => setFilters({ name: (e.target as HTMLInputElement).value })}
        />
        <Search
          placeholder="Group ID (uuid)"
          value={filters.analysisGroupId ?? ""}
          onChange={(e) =>
            setFilters({ analysisGroupId: (e.target as HTMLInputElement).value })
          }
        />
        <Search
          placeholder="Inspection Record ID (uuid)"
          value={filters.inspectionRecordId ?? ""}
          onChange={(e) =>
            setFilters({ inspectionRecordId: (e.target as HTMLInputElement).value })
          }
        />
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
            <Table.Cell>Name</Table.Cell>
            <Table.Cell>Created</Table.Cell>
            <Table.Cell>Group</Table.Cell>
            <Table.Cell>#Records</Table.Cell>
            <Table.Cell>#Runs</Table.Cell>
            <Table.Cell>Latest Run</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {showSkeleton ? (
            <TableSkeleton columns={8} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={8}>No analyses.</Table.Cell>
            </Table.Row>
          ) : (
            items.map((a) => {
              const runs = a.runs ?? [];
              const latest = runs[runs.length - 1];
              return (
                <Table.Row
                  key={a.id}
                  onClick={() => navigate(`/analyses/${a.id}`)}
                  style={{ cursor: "pointer" }}
                >
                  <Table.Cell>
                    <IdCell id={a.id} />
                  </Table.Cell>
                  <Table.Cell>{a.name}</Table.Cell>
                  <Table.Cell>{new Date(a.createdAt).toLocaleString()}</Table.Cell>
                  <Table.Cell>
                    {a.analysisGroupId ? (
                      <Button
                        variant="ghost"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/analysis-groups/${a.analysisGroupId}`);
                        }}
                      >
                        View
                      </Button>
                    ) : (
                      "–"
                    )}
                  </Table.Cell>
                  <Table.Cell>{(a.inspectionRecords ?? []).length}</Table.Cell>
                  <Table.Cell>{runs.length}</Table.Cell>
                  <Table.Cell>{latest ? <StatusChip status={latest.status} /> : "–"}</Table.Cell>
                  <Table.Cell>
                    <Button
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRerun(a.id);
                      }}
                    >
                      Rerun
                    </Button>
                    <Button
                      variant="ghost"
                      color="danger"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(a.id);
                      }}
                    >
                      Delete
                    </Button>
                  </Table.Cell>
                </Table.Row>
              );
            })
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
