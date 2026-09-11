import { useNavigate } from "react-router";
import type { MouseEvent } from "react";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { AnalysisRun } from "../../../api/client";
import IdCell from "../../../components/IdCell";
import StatusChip from "../../../components/StatusChip";
import TableSkeleton from "../../../components/TableSkeleton";
import { TableScroller } from "../../../components/Styles";
import { argoWorkflowUrl } from "../../../utils/argo";
import { formatElapsedDuration } from "../../../utils/duration";

interface AnalysisRunsTableProps {
  items: AnalysisRun[];
  initialLoading: boolean;
  pageSize: number;
  busy: boolean;
  onDelete: (id: string) => Promise<void>;
}

export default function AnalysisRunsTable({
  items, initialLoading, pageSize, busy, onDelete,
}: AnalysisRunsTableProps) {
  const navigate = useNavigate();

  return (
    <TableScroller>
      <Table style={{ width: "100%" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>Analysis</Table.Cell>
            <Table.Cell>Run #</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>Duration</Table.Cell>
            <Table.Cell>#Workflows</Table.Cell>
            <Table.Cell>Argo</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {initialLoading ? (
            <TableSkeleton columns={10} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={10}>No runs.</Table.Cell>
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
                      {r.analysis.analysisType}
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
                <Table.Cell>{formatElapsedDuration(r.startedAt, r.completedAt)}</Table.Cell>
                <Table.Cell>{(r.workflows ?? []).length}</Table.Cell>
                <Table.Cell>
                  {argoWorkflowUrl(r.workflows?.[0]?.argoWorkflowName) && (
                    <Typography
                      link
                      href={argoWorkflowUrl(r.workflows?.[0]?.argoWorkflowName)!}
                      target="_blank"
                      rel="noopener noreferrer"
                      onClick={(e: MouseEvent) => e.stopPropagation()}
                    >
                      View
                    </Typography>
                  )}
                </Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    color="danger"
                    disabled={busy}
                    onClick={(e) => {
                      e.stopPropagation();
                      onDelete(r.id);
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
  );
}
