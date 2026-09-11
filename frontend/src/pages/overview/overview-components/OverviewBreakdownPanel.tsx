import { Table } from "@equinor/eds-core-react";
import type { AnalysisTypeStat, WorkflowTypeStat } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import { statusColors } from "../../../components/Styles";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";
import OverviewDenseTable from "./OverviewDenseTable";

function formatDuration(seconds: number | null): string {
  if (seconds == null) return "\u2013";
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) return `${(seconds / 60).toFixed(1)}m`;
  return `${(seconds / 3600).toFixed(1)}h`;
}

export default function OverviewBreakdownPanel({ title, rows, emptyMessage }: {
  title: string;
  rows: (WorkflowTypeStat | AnalysisTypeStat)[];
  emptyMessage: string;
}) {
  return (
    <DashboardPanel title={title}>
      <OverviewDenseTable>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Total</Table.Cell>
            <Table.Cell>OK</Table.Cell>
            <Table.Cell>Fail</Table.Cell>
            <Table.Cell>Fail %</Table.Cell>
            <Table.Cell>Avg</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {rows.length === 0 ? (
            <Table.Row><Table.Cell colSpan={6}>{emptyMessage}</Table.Cell></Table.Row>
          ) : rows.map((stat) => (
            <Table.Row key={"workflowType" in stat ? stat.workflowType : stat.analysisType}>
              <Table.Cell>
                {"workflowType" in stat ? stat.workflowType : formatAnalysisType(stat.analysisType)}
              </Table.Cell>
              <Table.Cell>{stat.total}</Table.Cell>
              <Table.Cell>{stat.succeeded}</Table.Cell>
              <Table.Cell>{stat.failed}</Table.Cell>
              <Table.Cell style={{ color: stat.failureRate > 0 ? statusColors.error.text : undefined }}>
                {Math.round(stat.failureRate * 100)}%
              </Table.Cell>
              <Table.Cell>{formatDuration(stat.averageDurationSeconds)}</Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </OverviewDenseTable>
    </DashboardPanel>
  );
}
