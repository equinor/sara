import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { BenchmarkJob } from "../../../../api/benchmarkClient";
import { isBenchmarkJobActive } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import StatusChip from "../../../../components/StatusChip";
import { TableScroller } from "../../../../components/Styles";
import TableSkeleton from "../../../../components/TableSkeleton";
import { argoWorkflowUrl } from "../../../../utils/argo";
import { formatElapsedDuration } from "../../../../utils/duration";
import { formatJobMethodSummary } from "../benchmarkFormatting";

interface BenchmarkJobsTableProps {
  jobs: BenchmarkJob[];
  loading: boolean;
}

function formatJobDuration(job: BenchmarkJob): string {
  if (job.startedAt && job.finishedAt) {
    return formatElapsedDuration(job.startedAt, job.finishedAt);
  }
  if (job.startedAt && isBenchmarkJobActive(job.phase)) {
    return formatElapsedDuration(job.startedAt, new Date().toISOString());
  }
  return "–";
}

export default function BenchmarkJobsTable({ jobs, loading }: BenchmarkJobsTableProps) {
  const activeCount = jobs.filter((j) => isBenchmarkJobActive(j.phase)).length;
  const title =
    activeCount > 0 ? `Benchmark jobs (${activeCount} active)` : "Recent benchmark jobs";

  return (
    <DashboardPanel title={title} style={{ marginBottom: "1rem" }}>
      <TableScroller>
        <Table style={{ width: "100%" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Run ID</Table.Cell>
              <Table.Cell>Dataset</Table.Cell>
              <Table.Cell>Method</Table.Cell>
              <Table.Cell>Device</Table.Cell>
              <Table.Cell>Status</Table.Cell>
              <Table.Cell>Started</Table.Cell>
              <Table.Cell>Duration</Table.Cell>
              <Table.Cell>Argo</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {loading ? (
              <TableSkeleton columns={8} rows={3} />
            ) : jobs.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={8}>No benchmark jobs found in Argo.</Table.Cell>
              </Table.Row>
            ) : (
              jobs.map((job) => {
                const argoUrl = argoWorkflowUrl(job.workflowName);
                return (
                  <Table.Row key={job.workflowName}>
                    <Table.Cell>
                      <div>{job.runId}</div>
                      {job.message && (
                        <Typography variant="caption" color="danger">
                          {job.message}
                        </Typography>
                      )}
                    </Table.Cell>
                    <Table.Cell>
                      {job.datasetId} / {job.version} / {job.split}
                    </Table.Cell>
                    <Table.Cell>{formatJobMethodSummary(job)}</Table.Cell>
                    <Table.Cell>{job.device.toUpperCase()}</Table.Cell>
                    <Table.Cell>
                      <StatusChip status={job.phase} />
                    </Table.Cell>
                    <Table.Cell>
                      {job.startedAt
                        ? new Date(job.startedAt).toLocaleString()
                        : job.createdAt
                          ? new Date(job.createdAt).toLocaleString()
                          : "–"}
                    </Table.Cell>
                    <Table.Cell>{formatJobDuration(job)}</Table.Cell>
                    <Table.Cell>
                      {argoUrl ? (
                        <Button
                          variant="ghost"
                          as="a"
                          href={argoUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          Logs
                        </Button>
                      ) : (
                        job.workflowName
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
