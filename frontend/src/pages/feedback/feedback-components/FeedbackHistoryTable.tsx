import { Button, Table } from "@equinor/eds-core-react";
import { useNavigate } from "react-router";
import type { FeedbackHistory } from "../../../api/client";
import FeedbackChip from "../../../components/FeedbackChip";
import TableSkeleton from "../../../components/TableSkeleton";
import { TableScroller } from "../../../components/Styles";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";

export default function FeedbackHistoryTable({ items, initialLoading, pageSize }: {
  items: FeedbackHistory[];
  initialLoading: boolean;
  pageSize: number;
}) {
  const navigate = useNavigate();
  return (
    <TableScroller>
      <Table style={{ width: "100%", minWidth: "850px" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Result</Table.Cell>
            <Table.Cell>Analysis</Table.Cell>
            <Table.Cell>Run #</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Action</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {initialLoading ? (
            <TableSkeleton columns={5} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row><Table.Cell colSpan={5}>No submitted feedback matches these filters.</Table.Cell></Table.Row>
          ) : items.map((item) => (
            <Table.Row
              key={item.id}
              onClick={() => navigate(`/analysis-runs/${item.analysisRunId}`)}
              style={{ cursor: "pointer" }}
            >
              <Table.Cell><FeedbackChip isCorrect={item.isCorrect} /></Table.Cell>
              <Table.Cell>{formatAnalysisType(item.analysisType)}</Table.Cell>
              <Table.Cell>{item.runNumber}</Table.Cell>
              <Table.Cell>{item.startedAt ? new Date(item.startedAt).toLocaleString() : "\u2013"}</Table.Cell>
              <Table.Cell><Button variant="ghost">View run</Button></Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
    </TableScroller>
  );
}
