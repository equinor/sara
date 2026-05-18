import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { getAnalysisRun, type AnalysisRun } from "../../api/client";
import StatusChip from "../../components/StatusChip";

Icon.add({ arrow_back });

export default function AnalysisRunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [run, setRun] = useState<AnalysisRun | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    getAnalysisRun(id).then(setRun).catch((e) =>
      setError(e instanceof Error ? e.message : "Failed to load")
    );
  }, [id]);

  if (error)
    return (
      <Typography variant="body_short" style={{ color: "#eb0000" }}>
        {error}
      </Typography>
    );
  if (!run) return <Typography variant="body_short">Loading…</Typography>;

  const workflows = (run.workflows ?? [])
    .slice()
    .sort((a, b) => a.stepNumber - b.stepNumber);

  return (
    <div style={{ paddingTop: "1rem" }}>
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
        Analysis Run #{run.runNumber}
      </Typography>

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
                {run.analysis?.name ?? run.analysisId}
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
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Workflows ({workflows.length})
      </Typography>
      <Table>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Step</Table.Cell>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell></Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {workflows.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={6}>None.</Table.Cell>
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
                <Table.Cell>
                  <Button variant="ghost" onClick={() => navigate(`/workflows/${w.id}`)}>
                    View
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>
    </div>
  );
}
