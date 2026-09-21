import { Table, Typography } from "@equinor/eds-core-react";
import type { AnalysisResult } from "../../../api/client";
import SeverityChip from "../../../components/SeverityChip";
import { TableScroller } from "../../../components/Styles";

/** The API sends numerics fixed to 5 decimals; drop the padding for display. */
function formatValue(result: AnalysisResult) {
  if (result.value == null || result.value === "") return "–";
  const numeric = Number(result.value);
  const text = Number.isNaN(numeric) ? result.value : String(Number(numeric.toFixed(4)));
  return result.unit ? `${text} ${result.unit}` : text;
}

export default function AnalysisResultsTable({ results }: { results: AnalysisResult[] }) {
  return (
    <>
      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Results ({results.length})
      </Typography>
      <TableScroller>
        <Table style={{ marginBottom: "1.5rem" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Key</Table.Cell>
              <Table.Cell>Value</Table.Cell>
              <Table.Cell>Confidence</Table.Cell>
              <Table.Cell>Severity</Table.Cell>
              <Table.Cell>Measured</Table.Cell>
              <Table.Cell>Message</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {results.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={6}>No results recorded.</Table.Cell>
              </Table.Row>
            ) : (
              results.map((result) => (
                <Table.Row key={`${result.key}-${result.measuredAt}`}>
                  <Table.Cell>{result.key ?? "–"}</Table.Cell>
                  <Table.Cell>{formatValue(result)}</Table.Cell>
                  <Table.Cell>
                    {result.confidence != null
                      ? `${Number(result.confidence.toFixed(1))} %`
                      : "–"}
                  </Table.Cell>
                  <Table.Cell>
                    <SeverityChip severity={result.severity} />
                  </Table.Cell>
                  <Table.Cell>
                    {result.measuredAt ? new Date(result.measuredAt).toLocaleString() : "–"}
                  </Table.Cell>
                  <Table.Cell>{result.warning ?? "–"}</Table.Cell>
                </Table.Row>
              ))
            )}
          </Table.Body>
        </Table>
      </TableScroller>
    </>
  );
}
