import { Table } from "@equinor/eds-core-react";
import type { BenchmarkEvaluation } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import { TableScroller } from "../../../../components/Styles";

interface BenchmarkConfusionMatrixPanelProps {
  name: string;
  evaluation: BenchmarkEvaluation;
}

export default function BenchmarkConfusionMatrixPanel({
  name,
  evaluation,
}: BenchmarkConfusionMatrixPanelProps) {
  return (
    <DashboardPanel
      title={`Confusion matrix (${name})`}
      style={{ marginBottom: "1rem" }}
    >
      <TableScroller>
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Ground truth \ Predicted</Table.Cell>
              {evaluation.labels.map((label) => (
                <Table.Cell key={label}>{label}</Table.Cell>
              ))}
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {evaluation.labels.map((rowLabel, rowIdx) => (
              <Table.Row key={rowLabel}>
                <Table.Cell>
                  <strong>{rowLabel}</strong>
                </Table.Cell>
                {evaluation.confusion_matrix[rowIdx]?.map((cell, colIdx) => (
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
  );
}
