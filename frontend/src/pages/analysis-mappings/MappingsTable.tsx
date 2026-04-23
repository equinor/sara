import {
  Button,
  Table,
  Typography,
  Icon,
  Chip,
} from "@equinor/eds-core-react";
import { delete_to_trash } from "@equinor/eds-icons";
import type { AnalysisMapping } from "../../api/client";
import styled from "styled-components";

const StyledChipGroup = styled.div`
  display: flex;
  gap: 0.25rem;
  flex-wrap: wrap;
`;

Icon.add({ delete_to_trash });

interface MappingsTableProps {
  data: AnalysisMapping[];
  onDelete: (id: string) => void;
}

export default function MappingsTable({ data, onDelete }: MappingsTableProps) {
  return (
    <Table>
      <Table.Head>
        <Table.Row>
          <Table.Cell>Tag</Table.Cell>
          <Table.Cell>Inspection Description</Table.Cell>
          <Table.Cell>Analyses to Run</Table.Cell>
          <Table.Cell>Actions</Table.Cell>
        </Table.Row>
      </Table.Head>
      <Table.Body>
        {data.map((row) => (
          <Table.Row key={row.id}>
            <Table.Cell>{row.tag}</Table.Cell>
            <Table.Cell>{row.inspectionDescription}</Table.Cell>
            <Table.Cell>
              <StyledChipGroup>
                {row.analysesToBeRun.map((type) => (
                  <Chip key={type}>{type}</Chip>
                ))}
              </StyledChipGroup>
            </Table.Cell>
            <Table.Cell>
              <Button
                variant="ghost_icon"
                color="danger"
                onClick={() => onDelete(row.id)}
                aria-label="Delete"
              >
                <Icon name="delete_to_trash" />
              </Button>
            </Table.Cell>
          </Table.Row>
        ))}
        {data.length === 0 && (
          <Table.Row>
            <Table.Cell colSpan={4}>
              <Typography variant="body_short">
                No analysis mappings found.
              </Typography>
            </Table.Cell>
          </Table.Row>
        )}
      </Table.Body>
    </Table>
  );
}
