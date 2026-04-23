import {
  Button,
  Dialog,
  Table,
  Chip,
} from "@equinor/eds-core-react";
import type { AnalysisMapping, AnalysisType, PlantDataRequest } from "../../api/client";
import styled from "styled-components";

const StyledChipGroup = styled.div`
  display: flex;
  gap: 0.25rem;
  flex-wrap: wrap;
`;

interface MappingPair {
  tag: string;
  inspectionDescription: string;
  analysesToBeRun: AnalysisType[];
}

function uniqueMappingPairs(mappings: AnalysisMapping[]): MappingPair[] {
  const map = new Map<string, MappingPair>();
  for (const m of mappings) {
    const key = `${m.tag}|||${m.inspectionDescription}`;
    const existing = map.get(key);
    if (existing) {
      for (const a of m.analysesToBeRun) {
        if (!existing.analysesToBeRun.includes(a)) {
          existing.analysesToBeRun.push(a);
        }
      }
    } else {
      map.set(key, {
        tag: m.tag,
        inspectionDescription: m.inspectionDescription,
        analysesToBeRun: [...m.analysesToBeRun],
      });
    }
  }
  return [...map.values()];
}

interface MappingPickerDialogProps {
  open: boolean;
  onClose: () => void;
  mappings: AnalysisMapping[];
  onSelect: (update: Partial<PlantDataRequest>) => void;
}

export default function MappingPickerDialog({ open, onClose, mappings, onSelect }: MappingPickerDialogProps) {
  const pairs = uniqueMappingPairs(mappings);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      style={{ width: "min(700px, 90vw)" }}
    >
      <Dialog.Header>
        <Dialog.Title>Select from Analysis Mappings</Dialog.Title>
      </Dialog.Header>
      <Dialog.CustomContent>
        <Table style={{ width: "100%" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Tag</Table.Cell>
              <Table.Cell>Inspection Description</Table.Cell>
              <Table.Cell>Analyses to Run</Table.Cell>
              <Table.Cell></Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {pairs.map((p, i) => (
              <Table.Row key={i}>
                <Table.Cell>{p.tag}</Table.Cell>
                <Table.Cell>{p.inspectionDescription}</Table.Cell>
                <Table.Cell>
                  <StyledChipGroup>
                    {p.analysesToBeRun.map((a) => (
                      <Chip key={a}>{a}</Chip>
                    ))}
                  </StyledChipGroup>
                </Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      onSelect({
                        tagId: p.tag,
                        inspectionDescription: p.inspectionDescription,
                      });
                      onClose();
                    }}
                  >
                    Select
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      </Dialog.CustomContent>
      <Dialog.Actions>
        <Button variant="ghost" onClick={onClose}>
          Cancel
        </Button>
      </Dialog.Actions>
    </Dialog>
  );
}
