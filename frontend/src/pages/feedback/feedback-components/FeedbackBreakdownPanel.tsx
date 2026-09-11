import { Table } from "@equinor/eds-core-react";
import type { FeedbackSummary } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import TableSkeleton from "../../../components/TableSkeleton";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";

export default function FeedbackBreakdownPanel({ summary, loading }: {
  summary: FeedbackSummary | undefined;
  loading: boolean;
}) {
  return (
    <DashboardPanel title="Breakdown by analysis type" variant="feedback">
      <Table style={{ width: "100%" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Analysis</Table.Cell>
            <Table.Cell>Reviewed</Table.Cell>
            <Table.Cell>Correctness</Table.Cell>
            <Table.Cell>Coverage</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {loading ? (
            <TableSkeleton columns={4} rows={3} />
          ) : !summary ? (
            <Table.Row><Table.Cell colSpan={4}>Summary unavailable.</Table.Cell></Table.Row>
          ) : summary.perAnalysisType.length === 0 ? (
            <Table.Row><Table.Cell colSpan={4}>No runs in this window.</Table.Cell></Table.Row>
          ) : summary.perAnalysisType.map((stat) => (
            <Table.Row key={stat.analysisType}>
              <Table.Cell>{formatAnalysisType(stat.analysisType)}</Table.Cell>
              <Table.Cell>{stat.reviewed}</Table.Cell>
              <Table.Cell>{`${Math.round(stat.correctnessRate * 100)}%`}</Table.Cell>
              <Table.Cell>{`${Math.round(stat.reviewRate * 100)}%`}</Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
    </DashboardPanel>
  );
}
