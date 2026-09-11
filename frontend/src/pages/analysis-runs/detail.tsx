import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { getAnalysisRun } from "../../api/client";
import { useResourceDetail } from "../../api/queries";
import { argoWorkflowStepUrl, argoWorkflowUrl } from "../../utils/argo";
import { formatElapsedDuration } from "../../utils/duration";
import StatusChip from "../../components/StatusChip";
import { ErrorText, TableScroller } from "../../components/Styles";

Icon.add({ arrow_back });

export default function AnalysisRunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: run, error, isPending } = useResourceDetail("analysis-runs", id, getAnalysisRun);

  if (!id || (error && !run))
    return (
      <ErrorText variant="body_short">
        {!id ? "Missing analysis run ID" : error?.message ?? "Failed to load"}
      </ErrorText>
    );
  if (isPending || !run) return <Typography variant="body_short">Loading…</Typography>;

  const workflows = (run.workflows ?? [])
    .slice()
    .sort((a, b) => a.stepNumber - b.stepNumber);
  const argoUrl = argoWorkflowUrl(workflows[0]?.argoWorkflowName);

  return (
    <div style={{ paddingTop: "1rem" }}>
      {error && (
        <ErrorText variant="body_short" role="alert">
          {error.message}
        </ErrorText>
      )}
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
        Analysis Run #{run.runNumber}
      </Typography>

      <TableScroller>
        <Table style={{ marginBottom: "1.5rem" }}>
          <Table.Body>
            <Table.Row>
              <Table.Cell>ID</Table.Cell>
              <Table.Cell>{run.id}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Analysis</Table.Cell>
              <Table.Cell>
                <Button variant="ghost" onClick={() => navigate(`/analyses/${run.analysisId}`)}>
                  {run.analysis?.analysisType ?? run.analysisId}
                </Button>
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Status</Table.Cell>
              <Table.Cell>
                <StatusChip status={run.status} />
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Started</Table.Cell>
              <Table.Cell>
                {run.startedAt ? new Date(run.startedAt).toLocaleString() : "–"}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Completed</Table.Cell>
              <Table.Cell>
                {run.completedAt ? new Date(run.completedAt).toLocaleString() : "–"}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Duration</Table.Cell>
              <Table.Cell>{formatElapsedDuration(run.startedAt, run.completedAt)}</Table.Cell>
            </Table.Row>
            {argoUrl && (
              <Table.Row>
                <Table.Cell>Argo Workflow</Table.Cell>
                <Table.Cell>
                  <Typography link href={argoUrl} target="_blank" rel="noopener noreferrer">
                    View
                  </Typography>
                </Table.Cell>
              </Table.Row>
            )}
          </Table.Body>
        </Table>
      </TableScroller>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Workflows ({workflows.length})
      </Typography>
      <TableScroller>
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Step</Table.Cell>
              <Table.Cell>Type</Table.Cell>
              <Table.Cell>Status</Table.Cell>
              <Table.Cell>Started</Table.Cell>
              <Table.Cell>Completed</Table.Cell>
              <Table.Cell>Duration</Table.Cell>
              <Table.Cell></Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {workflows.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={7}>None.</Table.Cell>
              </Table.Row>
            ) : (
              workflows.map((w) => (
                <Table.Row key={w.id}>
                  <Table.Cell>{w.stepNumber}</Table.Cell>
                  <Table.Cell>{w.workflowType}</Table.Cell>
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
                    <Button variant="ghost" onClick={() => navigate(`/workflows/${w.id}`)}>
                      View
                    </Button>
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
                        style={{ marginLeft: "0.75rem" }}
                      >
                        Argo
                      </Typography>
                    )}
                  </Table.Cell>
                </Table.Row>
              ))
            )}
          </Table.Body>
        </Table>
      </TableScroller>
    </div>
  );
}
