import { useNavigate } from "react-router";
import type { MouseEvent } from "react";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { Workflow } from "../../../api/client";
import IdCell from "../../../components/IdCell";
import StatusChip from "../../../components/StatusChip";
import TableSkeleton from "../../../components/TableSkeleton";
import { TableScroller } from "../../../components/Styles";
import { argoWorkflowStepUrl } from "../../../utils/argo";
import { formatElapsedDuration } from "../../../utils/duration";

interface WorkflowsTableProps {
  items: Workflow[];
  initialLoading: boolean;
  pageSize: number;
  busy: boolean;
  onRetry: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

export default function WorkflowsTable({
  items, initialLoading, pageSize, busy, onRetry, onDelete,
}: WorkflowsTableProps) {
  const navigate = useNavigate();

  return (
    <TableScroller>
      <Table style={{ width: "100%" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Step</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>Duration</Table.Cell>
            <Table.Cell>Run</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {initialLoading ? (
            <TableSkeleton columns={9} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={9}>No workflows.</Table.Cell>
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
                <Table.Cell>{formatElapsedDuration(w.startedAt, w.completedAt)}</Table.Cell>
                <Table.Cell>
                  {w.analysisRunId && (
                    <Button
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/analysis-runs/${w.analysisRunId}`);
                      }}
                    >
                      View
                    </Button>
                  )}
                </Table.Cell>
                <Table.Cell>
                  {w.status === "Failed" && (
                    <Button
                      variant="ghost"
                      disabled={busy}
                      onClick={(e) => {
                        e.stopPropagation();
                        onRetry(w.id);
                      }}
                    >
                      Retry
                    </Button>
                  )}
                  {argoWorkflowStepUrl(w.argoWorkflowName, w.argoNodeId, w.argoWorkflowUid) && (
                    <Typography
                      link
                      href={argoWorkflowStepUrl(
                        w.argoWorkflowName,
                        w.argoNodeId,
                        w.argoWorkflowUid
                      )!}
                      target="_blank"
                      rel="noopener noreferrer"
                      onClick={(e: MouseEvent) => e.stopPropagation()}
                      style={{ marginRight: "0.75rem" }}
                    >
                      Argo
                    </Typography>
                  )}
                  <Button
                    variant="ghost"
                    color="danger"
                    disabled={busy}
                    onClick={(e) => {
                      e.stopPropagation();
                      onDelete(w.id);
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
