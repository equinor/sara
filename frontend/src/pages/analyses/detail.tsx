import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import {
  getAnalysis,
  rerunAnalysis,
} from "../../api/client";
import { useResourceDetail, useResourceMutation } from "../../api/queries";
import StatusChip from "../../components/StatusChip";
import { ErrorText, TableScroller } from "../../components/Styles";

Icon.add({ arrow_back });

export default function AnalysisDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: analysis, error, isPending } = useResourceDetail("analyses", id, getAnalysis);
  const rerunMutation = useResourceMutation(rerunAnalysis);

  const handleRerun = async () => {
    if (!id || rerunMutation.isPending) return;
    try {
      await rerunMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Rerun failed");
    }
  };

  if (!id || (error && !analysis))
    return (
      <ErrorText variant="body_short">
        {!id ? "Missing analysis ID" : error?.message ?? "Failed to load"}
      </ErrorText>
    );
  if (isPending || !analysis) return <Typography variant="body_short">Loading…</Typography>;

  const runs = (analysis.runs ?? []).slice().sort(
    (a, b) => (b.runNumber ?? 0) - (a.runNumber ?? 0)
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
          Analysis: {analysis.analysisType}
        </Typography>
        <Button onClick={handleRerun} disabled={rerunMutation.isPending}>
          {rerunMutation.isPending ? "Triggering…" : "Rerun"}
        </Button>
      </div>

      <TableScroller>
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
      </TableScroller>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Inspection Records ({(analysis.inspectionRecords ?? []).length})
      </Typography>
      <TableScroller>
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
      </TableScroller>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Runs
      </Typography>
      <TableScroller>
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
      </TableScroller>
    </div>
  );
}
