import { Typography, Table } from "@equinor/eds-core-react";
import type { PlantData } from "../../api/client";

function formatDate(date: string | null | undefined): string {
  if (!date) return "-";
  return new Date(date).toLocaleString();
}

export default function GeneralInfoTable({ data }: { data: PlantData }) {
  return (
    <div style={{ marginBottom: "2rem" }}>
      <Typography variant="h5" style={{ marginBottom: "0.75rem" }}>General Information</Typography>
      <Table style={{ width: "100%" }}>
        <Table.Body>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600, width: "200px" }}>ID</Table.Cell>
            <Table.Cell style={{ fontFamily: "monospace", fontSize: "0.85rem" }}>{data.id}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Inspection ID</Table.Cell>
            <Table.Cell>{data.inspectionId}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Installation Code</Table.Cell>
            <Table.Cell>{data.installationCode}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Tag</Table.Cell>
            <Table.Cell>{data.tag ?? "-"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Inspection Description</Table.Cell>
            <Table.Cell>{data.inspectionDescription ?? "-"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Robot Name</Table.Cell>
            <Table.Cell>{data.robotName ?? "-"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Coordinates</Table.Cell>
            <Table.Cell>{data.coordinates ?? "-"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Date Created</Table.Cell>
            <Table.Cell>{formatDate(data.dateCreated)}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Timestamp</Table.Cell>
            <Table.Cell>{formatDate(data.timestamp)}</Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>
    </div>
  );
}
