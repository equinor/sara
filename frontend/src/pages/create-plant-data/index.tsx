import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  TextField,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate } from "react-router";
import {
  createPlantData,
  getAnalysisMappings,
  type PlantDataRequest,
  type AnalysisMapping,
} from "../../api/client";
import MappingPickerDialog from "./MappingPickerDialog";
import BlobStorageFields from "./BlobStorageFields";
import styled from "styled-components";

const StyledBackNavRowLg = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
`;

const StyledFormContainer = styled.div`
  max-width: 640px;
  display: flex;
  flex-direction: column;
  gap: 1rem;
`;

const StyledTwoColumnGrid = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
`;

const StyledActionRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
`;

Icon.add({ arrow_back });

const emptyForm: PlantDataRequest = {
  inspectionId: "",
  installationCode: "",
  tagId: "",
  inspectionDescription: "",
  rawDataBlobStorageLocation: {
    storageAccount: "",
    blobContainer: "",
    blobName: "",
  },
};

export default function CreatePlantDataPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState<PlantDataRequest>(emptyForm);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mappings, setMappings] = useState<AnalysisMapping[]>([]);
  const [showMappingPicker, setShowMappingPicker] = useState(false);

  const fetchMappings = useCallback(async () => {
    try {
      const result = await getAnalysisMappings();
      setMappings(result);
    } catch {
      // non-critical
    }
  }, []);

  useEffect(() => {
    fetchMappings();
  }, [fetchMappings]);

  const navigateBack = () => navigate("/plant-data");

  const handleCreate = async () => {
    setCreating(true);
    setError(null);
    try {
      await createPlantData(form);
      navigateBack();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create plant data");
    } finally {
      setCreating(false);
    }
  };

  return (
    <div style={{ paddingTop: "1rem" }}>
      <StyledBackNavRowLg>
        <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
          <Icon name="arrow_back" />
        </Button>
        <Typography variant="h3">Create Plant Data</Typography>
      </StyledBackNavRowLg>

      {error && (
        <Typography
          variant="body_short"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      <StyledFormContainer>
        <TextField
          id="inspectionId"
          label="Inspection ID"
          value={form.inspectionId}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
            setForm({ ...form, inspectionId: e.target.value })
          }
        />
        <TextField
          id="installationCode"
          label="Installation Code"
          value={form.installationCode}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
            setForm({ ...form, installationCode: e.target.value })
          }
        />

        <StyledTwoColumnGrid>
          <TextField
            id="tagId"
            label="Tag ID"
            value={form.tagId}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({ ...form, tagId: e.target.value })
            }
          />
          <TextField
            id="inspectionDescription"
            label="Inspection Description"
            value={form.inspectionDescription}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({ ...form, inspectionDescription: e.target.value })
            }
          />
        </StyledTwoColumnGrid>
        {mappings.length > 0 && (
          <Button
            variant="ghost"
            onClick={() => setShowMappingPicker(true)}
            style={{ alignSelf: "flex-start", padding: 0, fontSize: "0.85rem" }}
          >
            Fill from analysis mapping
          </Button>
        )}

        <BlobStorageFields
          location={form.rawDataBlobStorageLocation}
          onChange={(location) =>
            setForm({ ...form, rawDataBlobStorageLocation: location })
          }
        />

        <StyledActionRow>
          <Button onClick={handleCreate} disabled={creating}>
            {creating ? "Creating..." : "Create"}
          </Button>
          <Button variant="ghost" onClick={navigateBack}>
            Cancel
          </Button>
        </StyledActionRow>
      </StyledFormContainer>

      <MappingPickerDialog
        open={showMappingPicker}
        onClose={() => setShowMappingPicker(false)}
        mappings={mappings}
        onSelect={(update) => setForm({ ...form, ...update })}
      />
    </div>
  );
}
