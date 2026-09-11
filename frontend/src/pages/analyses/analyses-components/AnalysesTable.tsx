import { useNavigate } from "react-router";
import { Button, Table } from "@equinor/eds-core-react";
import type { Analysis } from "../../../api/client";
import IdCell from "../../../components/IdCell";
import StatusChip from "../../../components/StatusChip";
import TableSkeleton from "../../../components/TableSkeleton";
import { TableScroller } from "../../../components/Styles";

interface AnalysesTableProps {
  items: Analysis[];
  initialLoading: boolean;
  pageSize: number;
  busy: boolean;
  onRerun: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

export default function AnalysesTable({
  items, initialLoading, pageSize, busy, onRerun, onDelete,
}: AnalysesTableProps) {
  const navigate = useNavigate();

  return (
    <TableScroller>
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
          {initialLoading ? (
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
                  <Table.Cell>{a.analysisType}</Table.Cell>
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
                      disabled={busy}
                      onClick={(e) => {
                        e.stopPropagation();
                        onRerun(a.id);
                      }}
                    >
                      Rerun
                    </Button>
                    <Button
                      variant="ghost"
                      color="danger"
                      disabled={busy}
                      onClick={(e) => {
                        e.stopPropagation();
                        onDelete(a.id);
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
