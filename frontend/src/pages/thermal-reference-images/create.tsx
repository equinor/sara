import { useState } from "react";
import {
  Button,
  Typography,
  TextField,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate } from "react-router";
import styled from "styled-components";
import {
  createThermalReferenceMetadata,
  type ThermalReferenceMetadataInput,
} from "../../api/client";

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

const StyledBlobSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
  border: 1px solid #dcdcdc;
  border-radius: 0.5rem;
`;

const StyledActionRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
`;

Icon.add({ arrow_back });

const emptyForm: ThermalReferenceMetadataInput = {
  tagId: "",
  installationCode: "",
  inspectionDescription: "",
  referenceBlobStorageDirectory: {
    blobContainer: "",
    blobName: "",
  },
};

export default function CreateThermalReferenceMetadataPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState<ThermalReferenceMetadataInput>(emptyForm);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const navigateBack = () => navigate("/thermal-reference-images");

  const handleCreate = async () => {
    setCreating(true);
    setError(null);
    try {
      await createThermalReferenceMetadata(form);
      navigateBack();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to create thermal reference metadata"
      );
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
        <Typography variant="h3">Create Thermal Reference Metadata</Typography>
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
          id="tagId"
          label="Tag ID"
          value={form.tagId}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
            setForm({ ...form, tagId: e.target.value })
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
        <TextField
          id="inspectionDescription"
          label="Inspection Description"
          value={form.inspectionDescription}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
            setForm({ ...form, inspectionDescription: e.target.value })
          }
        />

        <StyledBlobSection>
          <Typography variant="h6">Reference Blob Storage Directory</Typography>
          <TextField
            id="blobContainer"
            label="Blob Container"
            value={form.referenceBlobStorageDirectory.blobContainer}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({
                ...form,
                referenceBlobStorageDirectory: {
                  ...form.referenceBlobStorageDirectory,
                  blobContainer: e.target.value,
                },
              })
            }
          />
          <TextField
            id="blobDirectory"
            label="Blob Directory"
            value={form.referenceBlobStorageDirectory.blobName}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({
                ...form,
                referenceBlobStorageDirectory: {
                  ...form.referenceBlobStorageDirectory,
                  blobName: e.target.value,
                },
              })
            }
          />
        </StyledBlobSection>

        <StyledActionRow>
          <Button onClick={handleCreate} disabled={creating}>
            {creating ? "Creating..." : "Create"}
          </Button>
          <Button variant="ghost" onClick={navigateBack}>
            Cancel
          </Button>
        </StyledActionRow>
      </StyledFormContainer>
    </div>
  );
}
