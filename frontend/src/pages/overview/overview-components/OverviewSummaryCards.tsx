import styled from "styled-components";
import { DASHBOARD_WINDOWS, type DashboardSummary } from "../../../api/client";
import StatCard from "../../../components/StatCard";

const CardRow = styled.div`
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 0.75rem;
`;

export default function OverviewSummaryCards({ summary, windowHours }: {
  summary: DashboardSummary;
  windowHours: number;
}) {
  return (
    <CardRow>
      <StatCard
        title={`Succeeded (${DASHBOARD_WINDOWS.find((w) => w.hours === windowHours)?.label})`}
        value={summary.runStatusCounts.succeeded}
        tone="success"
      />
      <StatCard
        title="Failed"
        value={summary.runStatusCounts.failed}
        tone={summary.runStatusCounts.failed > 0 ? "error" : "default"}
      />
      <StatCard
        title="In Progress"
        value={summary.currentlyRunning.runs}
        tone="info"
        subtitle={`${summary.currentlyRunning.workflows} workflow step(s)`}
      />
      <StatCard
        title="Success Rate"
        value={`${Math.round(summary.successRate * 100)}%`}
        tone={summary.successRate >= 0.9 ? "success" : summary.successRate >= 0.6 ? "warning" : "error"}
      />
      <StatCard title="Inspections Ingested" value={summary.inspectionRecordsIngested} tone="default" />
      <StatCard title="Groups Pending" value={summary.analysisGroupCounts.pending} tone="info" />
      <StatCard title="Groups Complete" value={summary.analysisGroupCounts.complete} tone="success" />
      <StatCard
        title="Groups Timed Out"
        value={summary.analysisGroupCounts.timedOut}
        tone={summary.analysisGroupCounts.timedOut > 0 ? "error" : "default"}
      />
    </CardRow>
  );
}
