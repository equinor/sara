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
import TableSkeleton from "../../components/TableSkeleton";
import styled from "styled-components";

const StyledTableContainer = styled.div`
  max-height: calc(100vh - 22rem);
  overflow: auto;
  border: 1px solid #dcdcdc;
  border-radius: 4px;
`;

Icon.add({ play });

interface PlantDataTableProps {
  data: PlantData[];
  hasLoaded: boolean;
  loading: boolean;
  pageSize: number;
  triggeringId: string | null;
  onTriggerAnonymizer: (id: string) => void;
}

const COLUMN_COUNT = 8;

export default function PlantDataTable({
  data,
  hasLoaded,
  loading,
  pageSize,
  triggeringId,
  onTriggerAnonymizer,
}: PlantDataTableProps) {
  const navigate = useNavigate();

  const sortedData = [...data].sort(
    (a, b) => new Date(b.dateCreated).getTime() - new Date(a.dateCreated).getTime()
  );

  return (
    <StyledTableContainer>
      <Table style={{ width: "100%" }}>
        <Table.Head sticky>
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
          {loading ? (
            <TableSkeleton
              columns={COLUMN_COUNT}
              rows={Math.min(pageSize, 10)}
            />
          ) : hasLoaded && data.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={COLUMN_COUNT}>
                <Typography variant="body_short">
                  No plant data found.
                </Typography>
              </Table.Cell>
            </Table.Row>
          ) : (
            sortedData.map((row) => (
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
                    {triggeringId === row.id ? "Triggering..." : "Anonymize"}
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>
    </StyledTableContainer>
  );
}
