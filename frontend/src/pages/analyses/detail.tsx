import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import {
  getAnalysis,
  rerunAnalysis,
  type Analysis,
} from "../../api/client";
import StatusChip from "../../components/StatusChip";

Icon.add({ arrow_back });

export default function AnalysisDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = () => {
    if (!id) return;
    getAnalysis(id).then(setAnalysis).catch((e) =>
      setError(e instanceof Error ? e.message : "Failed to load")
    );
  };

  useEffect(load, [id]);

  const handleRerun = async () => {
    if (!id) return;
    setBusy(true);
    try {
      await rerunAnalysis(id);
      load();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Rerun failed");
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
  if (!analysis) return <Typography variant="body_short">Loading…</Typography>;

  const runs = (analysis.runs ?? []).slice().sort(
    (a, b) => (b.runNumber ?? 0) - (a.runNumber ?? 0)
  );

  return (
    <div style={{ paddingTop: "1rem" }}>
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
          Analysis: {analysis.name}
        </Typography>
        <Button onClick={handleRerun} disabled={busy}>
          {busy ? "Triggering…" : "Rerun"}
        </Button>
      </div>

      <Table style={{ marginBottom: "1.5rem" }}>
        <Table.Body>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>{analysis.id}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Anonymized data</Table.Cell>
            <Table.Cell>
              <Typography link href={analysis.anonymizedSAS}>
                Link
              </Typography></Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Visualized data</Table.Cell>
            <Table.Cell>
              {
                analysis.visualizedSAS ? (
                <Typography link href={analysis.visualizedSAS}>
                  Link
                </Typography>) : "-"
              }
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Created</Table.Cell>
            <Table.Cell>{new Date(analysis.createdAt).toLocaleString()}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Group</Table.Cell>
            <Table.Cell>
              {analysis.analysisGroupId ? (
                <Button
                  variant="ghost"
                  onClick={() => navigate(`/analysis-groups/${analysis.analysisGroupId}`)}
                >
                  {analysis.analysisGroupId}
                </Button>
              ) : (
                "–"
              )}
            </Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Inspection Records ({(analysis.inspectionRecords ?? []).length})
      </Typography>
      <Table style={{ marginBottom: "1.5rem" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Inspection ID</Table.Cell>
            <Table.Cell>Installation</Table.Cell>
            <Table.Cell>Tag</Table.Cell>
            <Table.Cell></Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {(analysis.inspectionRecords ?? []).length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={4}>None.</Table.Cell>
            </Table.Row>
          ) : (
            (analysis.inspectionRecords ?? []).map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>{r.inspectionId}</Table.Cell>
                <Table.Cell>{r.installationCode}</Table.Cell>
                <Table.Cell>{r.tag ?? "–"}</Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    onClick={() => navigate(`/inspection-records/${r.id}`)}
                  >
                    View
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Runs
      </Typography>
      <Table>
        <Table.Head>
          <Table.Row>
            <Table.Cell>#</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>#Workflows</Table.Cell>
            <Table.Cell></Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {runs.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={6}>No runs.</Table.Cell>
            </Table.Row>
          ) : (
            runs.map((r) => (
              <Table.Row key={r.id}>
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
                <Table.Cell>{(r.workflows ?? []).length}</Table.Cell>
                <Table.Cell>
                  <Button variant="ghost" onClick={() => navigate(`/analysis-runs/${r.id}`)}>
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
