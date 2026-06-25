import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { getWorkflow, retryWorkflow, type Workflow } from "../../api/client";
import StatusChip from "../../components/StatusChip";

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
  const [workflow, setWorkflow] = useState<Workflow | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = () => {
    if (!id) return;
    getWorkflow(id).then(setWorkflow).catch((e) =>
      setError(e instanceof Error ? e.message : "Failed to load")
    );
  };

  useEffect(load, [id]);

  const handleRetry = async () => {
    if (!id) return;
    setBusy(true);
    try {
      await retryWorkflow(id);
      load();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Retry failed");
    } finally {
      setBusy(false);
    }
  };

  if (error)
    return (
      <Typography variant="body_short" style={{ color: "#eb0000" }}>
        {error}
      </Typography>
    );
  if (!workflow) return <Typography variant="body_short">Loading…</Typography>;

  return (
    <div style={{ paddingTop: "1rem" }}>
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
          Workflow: {workflow.workflowType} (step {workflow.stepNumber})
        </Typography>
        {workflow.status === "Failed" && (
          <Button onClick={handleRetry} disabled={busy}>
            {busy ? "Retrying…" : "Retry"}
          </Button>
        )}
      </div>

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
              <Button
                variant="ghost"
                onClick={() => navigate(`/analysis-runs/${workflow.analysisRunId}`)}
              >
                {workflow.analysisRunId}
              </Button>
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
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Inputs
      </Typography>
      {workflow.inputBlobSAS.length === 0 ? (
        <Typography variant="body_short" style={{ marginBottom: "1.5rem" }}>
          None.
        </Typography>
      ) : (
        <ul style={{ marginBottom: "1.5rem" }}>
          {workflow.inputBlobSAS.map((loc, i) => (
            <li key={i}>
              <Typography link href={loc}>
                Link
              </Typography>
            </li>
          ))}
        </ul>
      )}

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
          <Typography variant="h5" style={{ marginBottom: "0.5rem", color: "#eb0000" }}>
            Error
          </Typography>
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
