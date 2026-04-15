import { Typography, Table } from "@equinor/eds-core-react";
import type { BlobStorageLocation } from "../../api/client";
import StatusChip from "../../components/StatusChip";
import BlobLocation from "./BlobLocation";

function formatDate(date: string | null | undefined): string {
  if (!date) return "-";
  return new Date(date).toLocaleString();
}

interface WorkflowSectionProps {
  title: string;
  workflow: {
    id: string;
    status: string;
    dateCreated: string;
    sourceBlobStorageLocation: BlobStorageLocation;
    destinationBlobStorageLocation: BlobStorageLocation;
  } | null | undefined;
  extraFields?: { label: string; value: string }[];
}

export default function WorkflowSection({ title, workflow, extraFields }: WorkflowSectionProps) {
  if (!workflow) {
    return (
      <div style={{ marginBottom: "1.5rem" }}>
        <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>{title}</Typography>
        <Typography variant="body_short" style={{ color: "#6f6f6f" }}>Not available</Typography>
      </div>
    );
  }

  return (
    <div style={{ marginBottom: "1.5rem" }}>
      <Typography variant="h5" style={{ marginBottom: "0.75rem" }}>{title}</Typography>
      <Table style={{ width: "100%" }}>
        <Table.Body>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600, width: "200px" }}>Status</Table.Cell>
            <Table.Cell>
              <StatusChip status={workflow.status} />
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Date Created</Table.Cell>
            <Table.Cell>{formatDate(workflow.dateCreated)}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Source</Table.Cell>
            <Table.Cell><BlobLocation location={workflow.sourceBlobStorageLocation} /></Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ fontWeight: 600 }}>Destination</Table.Cell>
            <Table.Cell><BlobLocation location={workflow.destinationBlobStorageLocation} /></Table.Cell>
          </Table.Row>
          {extraFields?.map((f) => (
            <Table.Row key={f.label}>
              <Table.Cell style={{ fontWeight: 600 }}>{f.label}</Table.Cell>
              <Table.Cell>{f.value}</Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
    </div>
  );
}
