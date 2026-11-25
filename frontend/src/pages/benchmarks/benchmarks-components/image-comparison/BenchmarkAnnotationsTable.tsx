import { Table } from "@equinor/eds-core-react";
import type { BenchmarkCocoAnnotation } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import { TableScroller } from "../../../../components/Styles";
import { GT_COLOR, LegendSwatch, PRED_COLOR } from "./BenchmarkMaskCanvas";

interface BenchmarkAnnotationsTableProps {
  groundTruth: BenchmarkCocoAnnotation[];
  predictions: BenchmarkCocoAnnotation[];
}

export default function BenchmarkAnnotationsTable({
  groundTruth,
  predictions,
}: BenchmarkAnnotationsTableProps) {
  const rows = [
    ...groundTruth.map((ann) => ({
      source: "Ground truth",
      color: GT_COLOR.stroke,
      ann,
    })),
    ...predictions.map((ann) => ({
      source: "Prediction",
      color: PRED_COLOR.stroke,
      ann,
    })),
  ];

  return (
    <DashboardPanel title="Instance annotations">
      <TableScroller>
        <Table style={{ width: "100%" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Source</Table.Cell>
              <Table.Cell>ID</Table.Cell>
              <Table.Cell>Class</Table.Cell>
              <Table.Cell>BBox [x, y, w, h]</Table.Cell>
              <Table.Cell>Area (px)</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {rows.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={5}>
                  No ground-truth or predicted instances (confirmed negative).
                </Table.Cell>
              </Table.Row>
            ) : (
              rows.map(({ source, color, ann }) => (
                <Table.Row key={`${source}-${ann.id}`}>
                  <Table.Cell>
                    <LegendSwatch $color={color} />
                    {source}
                  </Table.Cell>
                  <Table.Cell>{ann.id}</Table.Cell>
                  <Table.Cell>{ann.categoryName}</Table.Cell>
                  <Table.Cell>
                    {ann.bbox ? ann.bbox.map((v) => Math.round(v)).join(", ") : "–"}
                  </Table.Cell>
                  <Table.Cell>
                    {ann.area != null ? Math.round(ann.area) : "–"}
                  </Table.Cell>
                </Table.Row>
              ))
            )}
          </Table.Body>
        </Table>
      </TableScroller>
    </DashboardPanel>
  );
}
