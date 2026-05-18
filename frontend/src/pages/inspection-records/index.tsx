import { useNavigate } from "react-router";
import {
  Button,
  Search,
  Table,
  Typography,
} from "@equinor/eds-core-react";
import {
  deleteInspectionRecord,
  getInspectionRecords,
  type InspectionRecord,
  type InspectionRecordParams,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";

const FILTER_KEYS: (keyof InspectionRecordParams & string)[] = [
  "inspectionId",
  "tag",
  "installationCode",
];

function latestRunStatus(record: InspectionRecord): string | null {
  const runs = (record.analyses ?? []).flatMap((a) => a.runs ?? []);
  if (runs.length === 0) return null;
  runs.sort(
    (a, b) =>
      new Date(b.startedAt ?? 0).getTime() - new Date(a.startedAt ?? 0).getTime()
  );
  return runs[0].status;
}

export default function InspectionRecordsPage() {
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
  } = usePagedList<InspectionRecord, InspectionRecordParams>(
    "inspectionRecords.pageSize",
    FILTER_KEYS,
    getInspectionRecords
  );

  const items = response?.items ?? [];
  const showSkeleton = loading || (response === null && error === null);

  const handleDelete = async (id: string) => {
    if (!window.confirm("Delete this inspection record and its analyses?")) return;
    try {
      await deleteInspectionRecord(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader
      title="Inspection Records"
      loading={loading}
      onRefresh={refetch}
      primaryAction={{
        label: "New Inspection Record",
        onClick: () => navigate("/inspection-records/new"),
      }}
    >
      <div
        style={{
          display: "flex",
          gap: "0.75rem",
          marginBottom: "1rem",
          flexWrap: "wrap",
        }}
      >
        <Search
          placeholder="Inspection ID"
          value={filters.inspectionId ?? ""}
          onChange={(e) =>
            setFilters({ inspectionId: (e.target as HTMLInputElement).value })
          }
        />
        <Search
          placeholder="Tag"
          value={filters.tag ?? ""}
          onChange={(e) => setFilters({ tag: (e.target as HTMLInputElement).value })}
        />
        <Search
          placeholder="Installation"
          value={filters.installationCode ?? ""}
          onChange={(e) =>
            setFilters({ installationCode: (e.target as HTMLInputElement).value })
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
            <Table.Cell>Inspection ID</Table.Cell>
            <Table.Cell>Installation</Table.Cell>
            <Table.Cell>Tag</Table.Cell>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Created</Table.Cell>
            <Table.Cell>Group</Table.Cell>
            <Table.Cell>Analyses</Table.Cell>
            <Table.Cell>Latest Run</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {showSkeleton ? (
            <TableSkeleton columns={10} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={10}>
                <Typography variant="body_short">No inspection records.</Typography>
              </Table.Cell>
            </Table.Row>
          ) : (
            items.map((rec) => {
              const status = latestRunStatus(rec);
              return (
                <Table.Row
                  key={rec.id}
                  onClick={() => navigate(`/inspection-records/${rec.id}`)}
                  style={{ cursor: "pointer" }}
                >
                  <Table.Cell>
                    <IdCell id={rec.id} />
                  </Table.Cell>
                  <Table.Cell>{rec.inspectionId}</Table.Cell>
                  <Table.Cell>{rec.installationCode}</Table.Cell>
                  <Table.Cell>{rec.tag ?? "–"}</Table.Cell>
                  <Table.Cell>{rec.inspectionType ?? "–"}</Table.Cell>
                  <Table.Cell>{new Date(rec.createdAt).toLocaleString()}</Table.Cell>
                  <Table.Cell>
                    {rec.analysisGroupId ? (
                      <Button
                        variant="ghost"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/analysis-groups/${rec.analysisGroupId}`);
                        }}
                      >
                        View
                      </Button>
                    ) : (
                      "–"
                    )}
                  </Table.Cell>
                  <Table.Cell>{(rec.analyses ?? []).length}</Table.Cell>
                  <Table.Cell>{status ? <StatusChip status={status} /> : "–"}</Table.Cell>
                  <Table.Cell>
                    <Button
                      variant="ghost"
                      color="danger"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(rec.id);
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
