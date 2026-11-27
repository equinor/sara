import { useNavigate } from "react-router";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { BenchmarkResult } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import { ErrorText, TableScroller } from "../../../../components/Styles";
import TableSkeleton from "../../../../components/TableSkeleton";
import {
  benchmarkRunDetailPath,
  collectPrimaryMetricColumns,
  formatConfigurationSummary,
  formatMetricValue,
  formatNumber,
  shortArtifact,
} from "../benchmarkFormatting";

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
  const metricColumns = collectPrimaryMetricColumns(results);
  const totalColumns = 6 + metricColumns.length;
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
              <Table.Cell>Config</Table.Cell>
              <Table.Cell>Artifact</Table.Cell>
              <Table.Cell>Hardware</Table.Cell>
              <Table.Cell>Throughput</Table.Cell>
              {metricColumns.map((col) => (
                <Table.Cell key={col.key}>{col.label}</Table.Cell>
              ))}
              <Table.Cell>Details</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {loading ? (
              <TableSkeleton columns={totalColumns} rows={4} />
            ) : results.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={totalColumns}>
                  No saved benchmark results found for this split.
                </Table.Cell>
              </Table.Row>
            ) : (
              results.map((result) => {
                const configSummary = formatConfigurationSummary(result.run?.configuration);
                const metricsByKey = new Map(
                  (result.metrics?.summary_metrics ?? []).map((m) => [m.key, m])
                );
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
                    <Table.Cell>
                      {configSummary ? (
                        <>
                          <div>{configSummary.primary}</div>
                          {configSummary.details && (
                            <Typography variant="caption">
                              {configSummary.details}
                            </Typography>
                          )}
                        </>
                      ) : (
                        "–"
                      )}
                    </Table.Cell>
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
                    {metricColumns.map((col) => {
                      const metric = metricsByKey.get(col.key);
                      return (
                        <Table.Cell key={col.key}>
                          {metric ? formatMetricValue(metric.value, metric.format) : "–"}
                        </Table.Cell>
                      );
                    })}
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
