import {
  Button,
  Table,
  Typography,
  Icon,
} from "@equinor/eds-core-react";
import { play } from "@equinor/eds-icons";
import type { PlantData } from "../../api/client";
import { useNavigate } from "react-router";
import StatusChip from "../../components/StatusChip";

Icon.add({ play });

interface PlantDataTableProps {
  data: PlantData[];
  triggeringId: string | null;
  onTriggerAnonymizer: (id: string) => void;
}

export default function PlantDataTable({ data, triggeringId, onTriggerAnonymizer }: PlantDataTableProps) {
  const navigate = useNavigate();
  return (
    <Table>
      <Table.Head>
        <Table.Row>
          <Table.Cell>Inspection ID</Table.Cell>
          <Table.Cell>Installation Code</Table.Cell>
          <Table.Cell>Tag</Table.Cell>
          <Table.Cell>Description</Table.Cell>
          <Table.Cell>Robot</Table.Cell>
          <Table.Cell>Date Created</Table.Cell>
          <Table.Cell>Anonymization</Table.Cell>
          <Table.Cell>Actions</Table.Cell>
        </Table.Row>
      </Table.Head>
      <Table.Body>
        {data.map((row) => (
          <Table.Row key={row.id}>
            <Table.Cell>
              <Button
                variant="ghost"
                onClick={() => navigate(`/plant-data/${row.id}`)}
                style={{ padding: 0, textDecoration: "underline" }}
              >
                {row.inspectionId}
              </Button>
            </Table.Cell>
            <Table.Cell>{row.installationCode}</Table.Cell>
            <Table.Cell>{row.tag ?? "-"}</Table.Cell>
            <Table.Cell>{row.inspectionDescription ?? "-"}</Table.Cell>
            <Table.Cell>{row.robotName ?? "-"}</Table.Cell>
            <Table.Cell>
              {new Date(row.dateCreated).toLocaleString()}
            </Table.Cell>
            <Table.Cell>
              <StatusChip status={row.anonymization.status} />
            </Table.Cell>
            <Table.Cell>
              <Button
                variant="ghost"
                onClick={() => onTriggerAnonymizer(row.id)}
                disabled={
                  triggeringId === row.id ||
                  row.anonymization.status === "Started" ||
                  row.anonymization.status === "ExitSuccess"
                }
              >
                <Icon name="play" />
                {triggeringId === row.id
                  ? "Triggering..."
                  : "Anonymize"}
              </Button>
            </Table.Cell>
          </Table.Row>
        ))}
        {data.length === 0 && (
          <Table.Row>
            <Table.Cell colSpan={8}>
              <Typography variant="body_short">
                No plant data found.
              </Typography>
            </Table.Cell>
          </Table.Row>
        )}
      </Table.Body>
    </Table>
  );
}
