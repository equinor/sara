import { useState } from "react";
import {
  Button,
  Dialog,
  TextField,
  NativeSelect,
} from "@equinor/eds-core-react";
import {
  createAnalysisMapping,
  type AnalysisType,
} from "../../api/client";

const ANALYSIS_TYPES: AnalysisType[] = [
  "ConstantLevelOiler",
  "Fencilla",
  "ThermalReading",
];

interface CreateMappingDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
  onError: (msg: string) => void;
}

export default function CreateMappingDialog({ open, onClose, onCreated, onError }: CreateMappingDialogProps) {
  const [tagId, setTagId] = useState("");
  const [inspectionDescription, setInspectionDescription] = useState("");
  const [analysisType, setAnalysisType] = useState<AnalysisType>("ConstantLevelOiler");
  const [creating, setCreating] = useState(false);

  const handleCreate = async () => {
    setCreating(true);
    try {
      await createAnalysisMapping(tagId, inspectionDescription, analysisType);
      setTagId("");
      setInspectionDescription("");
      setAnalysisType("ConstantLevelOiler");
      onClose();
      onCreated();
    } catch (e) {
      onError(e instanceof Error ? e.message : "Failed to create analysis mapping");
    } finally {
      setCreating(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose}>
      <Dialog.Header>
        <Dialog.Title>Create Analysis Mapping</Dialog.Title>
      </Dialog.Header>
      <Dialog.CustomContent>
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <TextField
            id="tagId"
            label="Tag ID"
            value={tagId}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setTagId(e.target.value)
            }
          />
          <TextField
            id="inspectionDescription"
            label="Inspection Description"
            value={inspectionDescription}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setInspectionDescription(e.target.value)
            }
          />
          <NativeSelect
            id="analysisType"
            label="Analysis Type"
            value={analysisType}
            onChange={(e: React.ChangeEvent<HTMLSelectElement>) =>
              setAnalysisType(e.target.value as AnalysisType)
            }
          >
            {ANALYSIS_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </NativeSelect>
        </div>
      </Dialog.CustomContent>
      <Dialog.Actions>
        <Button onClick={handleCreate} disabled={creating}>
          {creating ? "Creating..." : "Create"}
        </Button>
        <Button variant="ghost" onClick={onClose}>
          Cancel
        </Button>
      </Dialog.Actions>
    </Dialog>
  );
}
