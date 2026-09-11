import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { NavigateFunction } from "react-router";
import type { InspectionRecord } from "../../../api/client";
import IdCell from "../../../components/IdCell";
import StatusChip from "../../../components/StatusChip";
import { TableScroller } from "../../../components/Styles";
import TableSkeleton from "../../../components/TableSkeleton";

function latestRunStatus(record: InspectionRecord): string | null {
  const runs = (record.analyses ?? []).flatMap((a) => a.runs ?? []);
  if (runs.length === 0) return null;
  runs.sort(
    (a, b) =>
      new Date(b.startedAt ?? 0).getTime() - new Date(a.startedAt ?? 0).getTime()
  );
  return runs[0].status;
}

interface InspectionRecordsTableProps {
  items: InspectionRecord[];
  initialLoading: boolean;
  pageSize: number;
  deleting: boolean;
  navigate: NavigateFunction;
  onDelete: (id: string) => Promise<void>;
}

export default function InspectionRecordsTable({
  items, initialLoading, pageSize, deleting, navigate, onDelete,
}: InspectionRecordsTableProps) {
  return (
    <TableScroller>
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
          {initialLoading ? (
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
                      disabled={deleting}
                      onClick={(e) => {
                        e.stopPropagation();
                        onDelete(rec.id);
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
    </TableScroller>
  );
}
