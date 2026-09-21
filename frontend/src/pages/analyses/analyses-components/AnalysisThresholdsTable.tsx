import { Table, Typography } from "@equinor/eds-core-react";
import type { AnalysisThreshold } from "../../../api/client";
import { TableScroller } from "../../../components/Styles";

const dash = (value: number | null | undefined) => (value != null ? String(value) : "–");

/** Describes the band in the same order the evaluator applies it. */
function describe(threshold: AnalysisThreshold) {
  if (threshold.alertWhenTrue != null)
    return `Alert when ${threshold.alertWhenTrue ? "true" : "false"}`;

  const parts: string[] = [];
  if (threshold.lowerAlert != null) parts.push(`Alert ≤ ${threshold.lowerAlert}`);
  if (threshold.lowerWarning != null) parts.push(`Warning ≤ ${threshold.lowerWarning}`);
  if (threshold.upperWarning != null) parts.push(`Warning ≥ ${threshold.upperWarning}`);
  if (threshold.upperAlert != null) parts.push(`Alert ≥ ${threshold.upperAlert}`);
  return parts.length > 0 ? parts.join(", ") : "No bounds — not evaluated";
}

export default function AnalysisThresholdsTable({
  thresholds,
}: {
  thresholds: AnalysisThreshold[];
}) {
  return (
    <>
      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Thresholds ({thresholds.length})
      </Typography>
      <TableScroller>
        <Table style={{ marginBottom: "1.5rem" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Key</Table.Cell>
              <Table.Cell>Lower alert</Table.Cell>
              <Table.Cell>Lower warning</Table.Cell>
              <Table.Cell>Upper warning</Table.Cell>
              <Table.Cell>Upper alert</Table.Cell>
              <Table.Cell>Min confidence</Table.Cell>
              <Table.Cell>Source</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {thresholds.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={7}>
                  No thresholds configured — results are recorded but not evaluated.
                </Table.Cell>
              </Table.Row>
            ) : (
              thresholds.map((threshold) => (
                <Table.Row key={threshold.id}>
                  <Table.Cell>
                    {threshold.key}
                    <Typography variant="caption" as="div">
                      {describe(threshold)}
                    </Typography>
                  </Table.Cell>
                  <Table.Cell>{dash(threshold.lowerAlert)}</Table.Cell>
                  <Table.Cell>{dash(threshold.lowerWarning)}</Table.Cell>
                  <Table.Cell>{dash(threshold.upperWarning)}</Table.Cell>
                  <Table.Cell>{dash(threshold.upperAlert)}</Table.Cell>
                  <Table.Cell>{dash(threshold.minConfidence)}</Table.Cell>
                  <Table.Cell>{threshold.source}</Table.Cell>
                </Table.Row>
              ))
            )}
          </Table.Body>
        </Table>
      </TableScroller>
    </>
  );
}
