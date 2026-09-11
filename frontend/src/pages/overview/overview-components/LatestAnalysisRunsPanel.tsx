import type { MouseEvent } from "react";
import { Table, Typography } from "@equinor/eds-core-react";
import { useNavigate } from "react-router";
import type { AnalysisRun } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import StatusChip from "../../../components/StatusChip";
import { argoWorkflowUrl } from "../../../utils/argo";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";
import OverviewDenseTable from "./OverviewDenseTable";

function formatTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString() : "\u2013";
}

export default function LatestAnalysisRunsPanel({ runs }: { runs: AnalysisRun[] }) {
  const navigate = useNavigate();
  // Show not-completed rows first, preserving recency within each group.
  const latestRuns = [...runs].sort(
    (a, b) => Number(a.completedAt != null) - Number(b.completedAt != null)
  );
  return (
    <DashboardPanel title="Latest analysis runs">
      <OverviewDenseTable>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Analysis</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>Argo</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {latestRuns.length === 0 ? (
            <Table.Row><Table.Cell colSpan={5}>No analysis runs.</Table.Cell></Table.Row>
          ) : latestRuns.map((run) => (
            <Table.Row
              key={run.id}
              onClick={() => navigate(`/analysis-runs/${run.id}`)}
              style={{ cursor: "pointer" }}
            >
              <Table.Cell>{formatTime(run.startedAt ?? null)}</Table.Cell>
              <Table.Cell>{formatAnalysisType(run.analysis?.analysisType)}</Table.Cell>
              <Table.Cell><StatusChip status={run.status} /></Table.Cell>
              <Table.Cell>{formatTime(run.completedAt ?? null)}</Table.Cell>
              <Table.Cell>
                {argoWorkflowUrl(run.workflows?.[0]?.argoWorkflowName) && (
                  <Typography
                    link
                    href={argoWorkflowUrl(run.workflows?.[0]?.argoWorkflowName)!}
                    target="_blank"
                    rel="noopener noreferrer"
                    onClick={(event: MouseEvent) => event.stopPropagation()}
                  >
                    View
                  </Typography>
                )}
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </OverviewDenseTable>
    </DashboardPanel>
  );
}
