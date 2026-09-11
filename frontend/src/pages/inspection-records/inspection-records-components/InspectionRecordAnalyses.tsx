import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { NavigateFunction } from "react-router";
import type { InspectionRecord } from "../../../api/client";
import StatusChip from "../../../components/StatusChip";
import { TableScroller } from "../../../components/Styles";

interface InspectionRecordAnalysesProps {
  analyses: InspectionRecord["analyses"];
  navigate: NavigateFunction;
}

export default function InspectionRecordAnalyses({ analyses, navigate }: InspectionRecordAnalysesProps) {
  return (
    <>
      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Analyses
      </Typography>
      <TableScroller>
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Type</Table.Cell>
              <Table.Cell>Created</Table.Cell>
              <Table.Cell>#Runs</Table.Cell>
              <Table.Cell>Latest Run</Table.Cell>
              <Table.Cell></Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {(analyses ?? []).length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={5}>No analyses.</Table.Cell>
              </Table.Row>
            ) : (
              (analyses ?? []).map((a) => {
                const runs = a.runs ?? [];
                const latest = runs[runs.length - 1];
                return (
                  <Table.Row key={a.id}>
                    <Table.Cell>{a.analysisType}</Table.Cell>
                    <Table.Cell>{new Date(a.createdAt).toLocaleString()}</Table.Cell>
                    <Table.Cell>{runs.length}</Table.Cell>
                    <Table.Cell>
                      {latest ? <StatusChip status={latest.status} /> : "–"}
                    </Table.Cell>
                    <Table.Cell>
                      <Button variant="ghost" onClick={() => navigate(`/analyses/${a.id}`)}>
                        View
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                );
              })
            )}
          </Table.Body>
        </Table>
      </TableScroller>
    </>
  );
}
