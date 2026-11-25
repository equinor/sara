import { useNavigate } from "react-router";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import type {
  BenchmarkEvaluation,
  BenchmarkResult,
} from "../../../api/benchmarkClient";
import DashboardPanel from "../../../components/DashboardPanel";
import { ErrorText, TableScroller } from "../../../components/Styles";
import TableSkeleton from "../../../components/TableSkeleton";

export interface EvaluationSummary {
  tp: number;
  fn: number;
  fp: number;
  precision: number | null;
  recall: number | null;
}

export function summarizeBinaryEvaluation(
  evaluation: BenchmarkEvaluation
): EvaluationSummary | null {
  const matrix = evaluation.confusion_matrix;
  if (matrix.length !== 2 || matrix[0].length !== 2 || matrix[1].length !== 2) {
    return null;
  }
  const tp = matrix[0][0] ?? 0;
  const fn = matrix[0][1] ?? 0;
  const fp = matrix[1][0] ?? 0;
  return {
    tp,
    fn,
    fp,
    precision: tp + fp > 0 ? tp / (tp + fp) : null,
    recall: tp + fn > 0 ? tp / (tp + fn) : null,
  };
}

export function formatPercent(value: number | null): string {
  return value === null ? "–" : `${(value * 100).toFixed(1)}%`;
}

export function formatNumber(value: number | null | undefined, digits = 2): string {
  return value == null ? "–" : value.toFixed(digits);
}

export function shortArtifact(artifact: string): string {
  const lastSlash = artifact.lastIndexOf("/");
  return lastSlash >= 0 ? artifact.slice(lastSlash + 1) : artifact;
}

export function benchmarkRunDetailPath(
  datasetId: string,
  version: string,
  split: string,
  runId: string
): string {
  return `/benchmarks/${encodeURIComponent(datasetId)}/${encodeURIComponent(version)}/${encodeURIComponent(split)}/${encodeURIComponent(runId)}`;
}

interface BenchmarkResultsTableProps {
  datasetId: string;
  version: string;
  split: string;
  results: BenchmarkResult[];
  loading: boolean;
}

export default function BenchmarkResultsTable({
  datasetId,
  version,
  split,
  results,
  loading,
}: BenchmarkResultsTableProps) {
  const navigate = useNavigate();

  const manifests = new Set(
    results.map((r) => r.run?.dataset.manifest_sha256).filter(Boolean)
  );
  const protocols = new Set(results.map((r) => r.run?.protocol).filter(Boolean));
  const title = version
    ? `Saved results (${datasetId} / ${version} / ${split})`
    : "Saved results";

  return (
    <DashboardPanel title={title}>
      {(manifests.size > 1 || protocols.size > 1) && (
        <Typography variant="caption" style={{ display: "block", marginBottom: "0.5rem" }}>
          Runs below use different dataset checksums or evaluation protocols; compare only
          runs with matching manifest SHA-256 and protocol.
        </Typography>
      )}
      <TableScroller>
        <Table style={{ width: "100%" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Run ID</Table.Cell>
              <Table.Cell>Artifact</Table.Cell>
              <Table.Cell>Hardware</Table.Cell>
              <Table.Cell>Throughput</Table.Cell>
              <Table.Cell>Eval</Table.Cell>
              <Table.Cell>Precision</Table.Cell>
              <Table.Cell>Recall</Table.Cell>
              <Table.Cell>Mean IoU</Table.Cell>
              <Table.Cell>Details</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {loading ? (
              <TableSkeleton columns={9} rows={4} />
            ) : results.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={9}>
                  No saved benchmark results found for this split.
                </Table.Cell>
              </Table.Row>
            ) : (
              results.map((result) => {
                const evalEntries = Object.entries(result.metrics?.evaluations ?? {});
                const primary = evalEntries[0];
                const summary = primary ? summarizeBinaryEvaluation(primary[1]) : null;
                const detailPath = benchmarkRunDetailPath(
                  datasetId,
                  version,
                  split,
                  result.runId
                );

                return (
                  <Table.Row
                    key={result.runId}
                    onClick={() => navigate(detailPath)}
                    style={{ cursor: "pointer" }}
                  >
                    <Table.Cell>{result.runId}</Table.Cell>
                    <Table.Cell title={result.run?.artifact ?? ""}>
                      {result.run ? shortArtifact(result.run.artifact) : "–"}
                    </Table.Cell>
                    <Table.Cell>
                      {result.run ? (
                        <>
                          <div>{result.run.execution.hardware}</div>
                          <Typography variant="caption">
                            {result.run.execution.timing_scope}
                          </Typography>
                        </>
                      ) : (
                        "–"
                      )}
                    </Table.Cell>
                    <Table.Cell>
                      {result.metrics ? (
                        <>
                          <div>
                            {formatNumber(result.metrics.processing.per_second, 2)}{" "}
                            {result.metrics.processing.unit}/s
                          </div>
                          <Typography variant="caption">
                            {result.metrics.processing.count} in{" "}
                            {formatNumber(result.metrics.processing.seconds, 1)}s
                          </Typography>
                        </>
                      ) : (
                        "–"
                      )}
                    </Table.Cell>
                    <Table.Cell>{primary ? primary[0] : "–"}</Table.Cell>
                    <Table.Cell>
                      {summary ? formatPercent(summary.precision) : "–"}
                    </Table.Cell>
                    <Table.Cell>{summary ? formatPercent(summary.recall) : "–"}</Table.Cell>
                    <Table.Cell>
                      {primary ? formatNumber(primary[1].mean_iou, 3) : "–"}
                    </Table.Cell>
                    <Table.Cell>
                      {result.error ? (
                        <ErrorText variant="caption">{result.error}</ErrorText>
                      ) : (
                        <Button
                          variant="ghost"
                          onClick={(e) => {
                            e.stopPropagation();
                            navigate(detailPath);
                          }}
                        >
                          Open
                        </Button>
                      )}
                    </Table.Cell>
                  </Table.Row>
                );
              })
            )}
          </Table.Body>
        </Table>
      </TableScroller>
    </DashboardPanel>
  );
}
