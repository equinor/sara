import { useState, useCallback } from "react";
import {
  Button,
  Typography,
  TextField,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate, useSearchParams } from "react-router";
import styled from "styled-components";
import {
  createThermalReferenceMetadata,
  createThermalReferenceFromInspectionRecord,
  getInspectionRecordThermalImage,
  type ThermalReferenceMetadataInput,
  type InspectionRecord,
  type ThermalImageData,
} from "../../api/client";
import ThermalInspectionRecordSelector from "../../components/ThermalInspectionRecordSelector";
import PolygonDrawingEditor from "../../components/PolygonDrawingEditor";

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

const SegmentedToggle = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  border: 1px solid #dcdcdc;
  border-radius: 0.5rem;
  overflow: hidden;
`;

const SegmentOption = styled.button<{ $active: boolean }>`
  all: unset;
  font-family: 'Equinor', sans-serif;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem 1.5rem;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  text-align: center;
  transition: background-color 0.15s, color 0.15s;
  background-color: ${({ $active }) => ($active ? "#007079" : "#ffffff")};
  color: ${({ $active }) => ($active ? "#ffffff" : "#3d3d3d")};
  border-right: 1px solid #dcdcdc;

  &:last-child {
    border-right: none;
  }

  &:hover {
    background-color: ${({ $active }) => ($active ? "#005f66" : "#f7f7f7")};
  }
`;

const StyledImageSection = styled.div`
  margin-top: 1rem;
  position: relative;
`;

const LoadingOverlay = styled.div`
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: rgba(255, 255, 255, 0.6);
  z-index: 1;
  border-radius: 0.25rem;
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
  const [searchParams, setSearchParams] = useSearchParams();
  const fromInspection = searchParams.get("mode") === "inspection";
  const [form, setForm] = useState<ThermalReferenceMetadataInput>(emptyForm);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Inspection record selection state
  const [selectedRecord, setSelectedRecord] = useState<InspectionRecord | null>(
    null
  );
  const [thermalImageData, setThermalImageData] =
    useState<ThermalImageData | null>(null);
  const [loadingImage, setLoadingImage] = useState(false);
  const [polygon, setPolygon] = useState<number[][]>([]);

  const navigateBack = () => navigate("/thermal-reference-images");

  const handleRecordSelect = useCallback(
    async (record: InspectionRecord) => {
      setSelectedRecord(record);
      setPolygon([]);
      setError(null);

      // Auto-fill metadata fields
      setForm((prev) => ({
        ...prev,
        tagId: record.tag ?? prev.tagId,
        installationCode: record.installationCode ?? prev.installationCode,
        inspectionDescription:
          record.inspectionDescription ?? prev.inspectionDescription,
      }));

      // Fetch thermal image
      setLoadingImage(true);
      try {
        const imageData = await getInspectionRecordThermalImage(record.id);
        setThermalImageData(imageData);
      } catch (e) {
        setError(
          e instanceof Error
            ? e.message
            : "Failed to load thermal image from inspection record"
        );
      } finally {
        setLoadingImage(false);
      }
    },
    []
  );

  const handleCreateManual = async () => {
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

  const handleCreateFromInspection = async () => {
    if (!selectedRecord) return;
    if (polygon.length < 3) {
      setError("Please draw a polygon on the thermal image before submitting.");
      return;
    }

    setCreating(true);
    setError(null);
    try {
      const result = await createThermalReferenceFromInspectionRecord({
        inspectionRecordId: selectedRecord.id,
        tagId: form.tagId,
        installationCode: form.installationCode,
        inspectionDescription: form.inspectionDescription,
        polygon,
      });
      navigate(`/thermal-reference-images/${result.id}`);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to create thermal reference metadata from inspection record"
      );
    } finally {
      setCreating(false);
    }
  };

  const canSubmitFromInspection =
    selectedRecord !== null &&
    polygon.length >= 3 &&
    form.tagId.trim() !== "" &&
    form.installationCode.trim() !== "" &&
    form.inspectionDescription.trim() !== "";

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

        <SegmentedToggle>
          <SegmentOption
            $active={!fromInspection}
            onClick={() => {
              if (fromInspection) {
                setSearchParams({});
                setSelectedRecord(null);
                setThermalImageData(null);
                setPolygon([]);
                setError(null);
              }
            }}
          >
            Manual blob path
          </SegmentOption>
          <SegmentOption
            $active={fromInspection}
            onClick={() => {
              if (!fromInspection) {
                setSearchParams({ mode: "inspection" });
                setError(null);
              }
            }}
          >
            From inspection record
          </SegmentOption>
        </SegmentedToggle>

        {!fromInspection && (
          <>
            <StyledBlobSection>
              <Typography variant="h6">
                Reference Blob Storage Directory
              </Typography>
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
              <Button onClick={handleCreateManual} disabled={creating}>
                {creating ? "Creating..." : "Create"}
              </Button>
              <Button variant="ghost" onClick={navigateBack}>
                Cancel
              </Button>
            </StyledActionRow>
          </>
        )}

        {fromInspection && (
          <>
            <ThermalInspectionRecordSelector
              onSelect={handleRecordSelect}
              selectedId={selectedRecord?.id}
            />

            {thermalImageData && (
              <StyledImageSection>
                {loadingImage && (
                  <LoadingOverlay>
                    <Typography variant="body_short">
                      Loading thermal image...
                    </Typography>
                  </LoadingOverlay>
                )}
                <PolygonDrawingEditor
                  temperatures={thermalImageData.temperatures}
                  width={thermalImageData.width}
                  height={thermalImageData.height}
                  minTemperature={thermalImageData.minTemperature}
                  maxTemperature={thermalImageData.maxTemperature}
                  onPolygonChange={setPolygon}
                />
              </StyledImageSection>
            )}

            {!thermalImageData && loadingImage && (
              <StyledImageSection>
                <Typography variant="body_short">
                  Loading thermal image...
                </Typography>
              </StyledImageSection>
            )}

            <StyledActionRow>
              <Button
                onClick={handleCreateFromInspection}
                disabled={creating || !canSubmitFromInspection}
              >
                {creating ? "Creating..." : "Create"}
              </Button>
              <Button variant="ghost" onClick={navigateBack}>
                Cancel
              </Button>
            </StyledActionRow>
          </>
        )}
      </StyledFormContainer>
    </div>
  );
}
