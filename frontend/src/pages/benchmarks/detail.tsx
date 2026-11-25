import { useNavigate, useParams } from "react-router";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import { useBenchmarkRunDetail } from "../../api/benchmarkClient";
import DashboardPanel from "../../components/DashboardPanel";
import PageHeader from "../../components/PageHeader";
import StatCard from "../../components/StatCard";
import { ErrorText, TableScroller } from "../../components/Styles";
import BenchmarkPerImageTable from "./benchmarks-components/BenchmarkPerImageTable";
import {
  benchmarkRunDetailPath,
  formatNumber,
  formatPercent,
  shortArtifact,
  summarizeBinaryEvaluation,
} from "./benchmarks-components/BenchmarkResultsTable";

const StatsGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.75rem;
  margin-bottom: 1rem;
`;

const MetadataGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 0.75rem;
`;

const MetadataValue = styled(Typography).attrs({ variant: "body_short" })`
  word-break: break-word;
`;

export default function BenchmarkDetailPage() {
  const navigate = useNavigate();
  const { datasetId = "", version = "", split = "", runId = "" } = useParams();

  const detailQuery = useBenchmarkRunDetail(datasetId, version, split, runId);
  const detail = detailQuery.data;

  const evalEntries = Object.entries(detail?.metrics?.evaluations ?? {});
  const primary = evalEntries[0];
  const summary = primary ? summarizeBinaryEvaluation(primary[1]) : null;

  const handleSelectImage = (imageId: number) => {
    navigate(
      `${benchmarkRunDetailPath(datasetId, version, split, runId)}/images/${imageId}`
    );
  };

  return (
    <PageHeader
      title={`Benchmark: ${runId}`}
      loading={detailQuery.isFetching}
      onRefresh={() => detailQuery.refetch()}
    >
      <div style={{ marginBottom: "1rem" }}>
        <Button variant="ghost" onClick={() => navigate("/benchmarks")}>
          ← Back to Benchmarks
        </Button>
      </div>

      {detailQuery.error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {detailQuery.error.message}
        </ErrorText>
      )}
      {detail?.error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {detail.error}
        </ErrorText>
      )}

      {detail?.run && detail?.metrics && (
        <>
          <StatsGrid>
            <StatCard
              title="Throughput"
              value={`${formatNumber(detail.metrics.processing.per_second, 2)} ${detail.metrics.processing.unit}/s`}
              subtitle={`${detail.metrics.processing.count} in ${formatNumber(detail.metrics.processing.seconds, 1)}s`}
            />
            <StatCard
              title="Precision"
              value={summary ? formatPercent(summary.precision) : "–"}
              subtitle={summary ? `TP ${summary.tp} / FP ${summary.fp}` : undefined}
            />
            <StatCard
              title="Recall"
              value={summary ? formatPercent(summary.recall) : "–"}
              subtitle={summary ? `TP ${summary.tp} / FN ${summary.fn}` : undefined}
            />
            <StatCard
              title="Mean IoU"
              value={primary ? formatNumber(primary[1].mean_iou, 3) : "–"}
              subtitle={primary ? `${primary[1].matched_count} matched` : undefined}
            />
          </StatsGrid>

          <DashboardPanel title="Run metadata" style={{ marginBottom: "1rem" }}>
            <MetadataGrid>
              <div>
                <Typography variant="caption">Dataset</Typography>
                <MetadataValue>
                  {detail.run.dataset.id} / {detail.run.dataset.version} /{" "}
                  {detail.run.dataset.split}
                </MetadataValue>
              </div>
              <div>
                <Typography variant="caption">Artifact</Typography>
                <MetadataValue title={detail.run.artifact}>
                  {shortArtifact(detail.run.artifact)}
                </MetadataValue>
              </div>
              <div>
                <Typography variant="caption">Hardware</Typography>
                <MetadataValue>
                  {detail.run.execution.hardware} ({detail.run.execution.timing_scope})
                </MetadataValue>
              </div>
              <div>
                <Typography variant="caption">Protocol</Typography>
                <MetadataValue>{detail.run.protocol}</MetadataValue>
              </div>
              <div>
                <Typography variant="caption">Manifest SHA-256</Typography>
                <MetadataValue style={{ wordBreak: "break-all" }}>
                  {detail.run.dataset.manifest_sha256}
                </MetadataValue>
              </div>
            </MetadataGrid>
          </DashboardPanel>

          {evalEntries.map(([name, ev]) => (
            <DashboardPanel
              key={name}
              title={`Confusion matrix (${name})`}
              style={{ marginBottom: "1rem" }}
            >
              <TableScroller>
                <Table>
                  <Table.Head>
                    <Table.Row>
                      <Table.Cell>Ground truth \ Predicted</Table.Cell>
                      {ev.labels.map((label) => (
                        <Table.Cell key={label}>{label}</Table.Cell>
                      ))}
                    </Table.Row>
                  </Table.Head>
                  <Table.Body>
                    {ev.labels.map((rowLabel, rowIdx) => (
                      <Table.Row key={rowLabel}>
                        <Table.Cell>
                          <strong>{rowLabel}</strong>
                        </Table.Cell>
                        {ev.confusion_matrix[rowIdx]?.map((cell, colIdx) => (
                          <Table.Cell key={colIdx}>
                            {cell === null ? "–" : cell}
                          </Table.Cell>
                        ))}
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table>
              </TableScroller>
            </DashboardPanel>
          ))}
        </>
      )}

      {detail?.perImage ? (
        <BenchmarkPerImageTable
          items={detail.perImage}
          onSelectImage={handleSelectImage}
        />
      ) : (
        detail && (
          <DashboardPanel title="Per-image results">
            <Typography variant="body_short">
              No per-image breakdown was recorded for this run.
            </Typography>
          </DashboardPanel>
        )
      )}
    </PageHeader>
  );
}
