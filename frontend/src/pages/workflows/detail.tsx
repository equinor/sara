import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { getWorkflow, retryWorkflow } from "../../api/client";
import { useResourceDetail, useResourceMutation } from "../../api/queries";
import { argoWorkflowStepUrl } from "../../utils/argo";
import StatusChip from "../../components/StatusChip";
import { ErrorText, TableScroller } from "../../components/Styles";

Icon.add({ arrow_back });

function formatJson(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

export default function WorkflowDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: workflow, error, isPending } = useResourceDetail("workflows", id, getWorkflow);
  const retryMutation = useResourceMutation(retryWorkflow);

  const handleRetry = async () => {
    if (!id || retryMutation.isPending) return;
    try {
      await retryMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Retry failed");
    }
  };

  if (!id || (error && !workflow))
    return (
      <ErrorText variant="body_short">
        {!id ? "Missing workflow ID" : error?.message ?? "Failed to load"}
      </ErrorText>
    );
  if (isPending || !workflow) return <Typography variant="body_short">Loading…</Typography>;

  const argoUrl = argoWorkflowStepUrl(
    workflow.argoWorkflowName,
    workflow.argoNodeId,
    workflow.argoWorkflowUid
  );

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
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
          Workflow: {workflow.workflowType} (step {workflow.stepNumber})
        </Typography>
        {workflow.status === "Failed" && (
          <Button onClick={handleRetry} disabled={retryMutation.isPending}>
            {retryMutation.isPending ? "Retrying…" : "Retry"}
          </Button>
        )}
      </div>

      <TableScroller>
        <Table style={{ marginBottom: "1.5rem" }}>
          <Table.Body>
            <Table.Row>
              <Table.Cell>ID</Table.Cell>
              <Table.Cell>{workflow.id}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Status</Table.Cell>
              <Table.Cell>
                <StatusChip status={workflow.status} />
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Analysis Run</Table.Cell>
              <Table.Cell>
                {workflow.analysisRunId ? (
                  <Button
                    variant="ghost"
                    onClick={() => navigate(`/analysis-runs/${workflow.analysisRunId}`)}
                  >
                    {workflow.analysisRunId}
                  </Button>
                ) : (
                  "–"
                )}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Started</Table.Cell>
              <Table.Cell>
                {workflow.startedAt ? new Date(workflow.startedAt).toLocaleString() : "–"}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Completed</Table.Cell>
              <Table.Cell>
                {workflow.completedAt ? new Date(workflow.completedAt).toLocaleString() : "–"}
              </Table.Cell>
            </Table.Row>
            {argoUrl && (
              <Table.Row>
                <Table.Cell>Argo Workflow</Table.Cell>
                <Table.Cell>
                  <Typography link href={argoUrl} target="_blank" rel="noopener noreferrer">
                    {workflow.argoWorkflowName}
                  </Typography>
                </Table.Cell>
              </Table.Row>
            )}
          </Table.Body>
        </Table>
      </TableScroller>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Output
      </Typography>
      <div style={{ marginBottom: "1.5rem" }}>
        {workflow.outputBlobSAS ? (
          <Typography link href={workflow.outputBlobSAS}>
            Link
          </Typography>
        ) : (
          <Typography variant="body_short">None.</Typography>
        )}
      </div>

      {workflow.errorMessage && (
        <>
          <ErrorText variant="h5" style={{ marginBottom: "0.5rem" }}>
            Error
          </ErrorText>
          <pre
            style={{
              background: "#fff4f4",
              padding: "0.75rem",
              borderRadius: "4px",
              whiteSpace: "pre-wrap",
              marginBottom: "1.5rem",
            }}
          >
            {workflow.errorMessage}
          </pre>
        </>
      )}

      {workflow.resultJson && (
        <>
          <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
            Result JSON
          </Typography>
          <pre
            style={{
              background: "#f5f5f5",
              padding: "0.75rem",
              borderRadius: "4px",
              overflow: "auto",
              maxHeight: "400px",
            }}
          >
            {formatJson(workflow.resultJson)}
          </pre>
        </>
      )}
    </div>
  );
}
