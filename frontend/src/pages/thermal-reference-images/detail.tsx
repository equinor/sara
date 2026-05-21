import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  TextField,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate, useParams } from "react-router";
import styled from "styled-components";
import {
  getThermalReferenceMetadataById,
  updateThermalReferenceMetadata,
  deleteThermalReferenceMetadata,
  getThermalReferenceImageData,
  type ThermalReferenceMetadata,
  type ThermalReferenceMetadataInput,
  type ThermalImageData,
  type BlobStorageLocation,
} from "../../api/client";
import ThermalImageViewer from "../../components/ThermalImageViewer";

const StyledBackNavRowLg = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
`;

const StyledDetailGrid = styled.div`
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 0.5rem 1rem;
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

const StyledImageSection = styled.div`
  margin-bottom: 1.5rem;
`;

Icon.add({ arrow_back });

function formatBlobLocation(location: BlobStorageLocation) {
  return `${location.storageAccount}/${location.blobContainer}/${location.blobName}`;
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <>
      <Typography variant="body_short_bold">{label}</Typography>
      <Typography variant="body_short">{value}</Typography>
    </>
  );
}

export default function ThermalReferenceMetadataDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<ThermalReferenceMetadata | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<ThermalReferenceMetadataInput>({
    tagId: "",
    installationCode: "",
    inspectionDescription: "",
    referenceBlobStorageDirectory: { blobContainer: "", blobName: "" },
  });
  const [thermalImage, setThermalImage] = useState<ThermalImageData | null>(
    null
  );
  const [imageLoading, setImageLoading] = useState(false);
  const [imageError, setImageError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const result = await getThermalReferenceMetadataById(id);
      setData(result);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to fetch thermal reference metadata"
      );
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setImageLoading(true);
    setImageError(null);
    setThermalImage(null);
    getThermalReferenceImageData(id)
      .then((result) => {
        if (!cancelled) setThermalImage(result);
      })
      .catch((e) => {
        if (!cancelled)
          setImageError(
            e instanceof Error ? e.message : "Failed to load thermal image"
          );
      })
      .finally(() => {
        if (!cancelled) setImageLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  const navigateBack = () => navigate("/thermal-reference-images");

  const startEditing = () => {
    if (!data) return;
    setForm({
      tagId: data.tagId,
      installationCode: data.installationCode,
      inspectionDescription: data.inspectionDescription,
      referenceBlobStorageDirectory: {
        blobContainer: data.referenceImageBlobStorageLocation.blobContainer,
        blobName: "",
      },
    });
    setEditing(true);
  };

  const handleSave = async () => {
    if (!id) return;
    setSaving(true);
    setError(null);
    try {
      await updateThermalReferenceMetadata(id, form);
      setEditing(false);
      await fetchData();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to update thermal reference metadata"
      );
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!id) return;
    try {
      await deleteThermalReferenceMetadata(id);
      navigateBack();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to delete thermal reference metadata"
      );
    }
  };

  if (loading) {
    return (
      <div style={{ paddingTop: "1rem" }}>
        <Typography variant="body_short">Loading...</Typography>
      </div>
    );
  }

  if (!data) {
    return (
      <div style={{ paddingTop: "1rem" }}>
        <StyledBackNavRowLg>
          <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
            <Icon name="arrow_back" />
          </Button>
          <Typography variant="h3">Not Found</Typography>
        </StyledBackNavRowLg>
        {error && (
          <Typography
            variant="body_short"
            style={{ color: "#eb0000" }}
          >
            {error}
          </Typography>
        )}
      </div>
    );
  }

  return (
    <div style={{ paddingTop: "1rem" }}>
      <StyledBackNavRowLg>
        <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
          <Icon name="arrow_back" />
        </Button>
        <Typography variant="h3">Thermal Reference Metadata</Typography>
      </StyledBackNavRowLg>

      {error && (
        <Typography
          variant="body_short"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      <StyledImageSection>
        <Typography variant="h5" style={{ marginBottom: "0.75rem" }}>
          Reference Image
        </Typography>
        {imageLoading && (
          <Typography variant="body_short">Loading image...</Typography>
        )}
        {imageError && (
          <Typography variant="body_short" style={{ color: "#eb0000" }}>
            {imageError}
          </Typography>
        )}
        {thermalImage && (
          <ThermalImageViewer
            temperatures={thermalImage.temperatures}
            width={thermalImage.width}
            height={thermalImage.height}
            minTemperature={thermalImage.minTemperature}
            maxTemperature={thermalImage.maxTemperature}
          />
        )}
      </StyledImageSection>

      {editing ? (
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
            <Button onClick={handleSave} disabled={saving}>
              {saving ? "Saving..." : "Save"}
            </Button>
            <Button variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
          </StyledActionRow>
        </StyledFormContainer>
      ) : (
        <>
          <StyledDetailGrid>
            <DetailRow label="Tag ID" value={data.tagId} />
            <DetailRow label="Installation Code" value={data.installationCode} />
            <DetailRow label="Inspection Description" value={data.inspectionDescription} />
            <DetailRow
              label="Date Created"
              value={new Date(data.dateCreated).toLocaleString()}
            />
            <DetailRow
              label="Reference Image"
              value={formatBlobLocation(data.referenceImageBlobStorageLocation)}
            />
            <DetailRow
              label="Reference Polygon"
              value={formatBlobLocation(data.referencePolygonBlobStorageLocation)}
            />
          </StyledDetailGrid>

          <StyledActionRow>
            <Button onClick={startEditing}>Edit</Button>
            <Button variant="ghost" color="danger" onClick={handleDelete}>
              Delete
            </Button>
          </StyledActionRow>
        </>
      )}
    </div>
  );
}
