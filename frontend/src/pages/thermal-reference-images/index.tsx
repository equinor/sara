import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Button,
  Dialog,
  Icon,
  Table,
  TextField,
  Typography,
} from "@equinor/eds-core-react";
import { add, refresh } from "@equinor/eds-icons";
import styled from "styled-components";
import {
  createThermalReferenceMetadata,
  deleteThermalReferenceMetadata,
  getThermalReferenceMetadata,
  updateThermalReferenceMetadata,
  type BlobStorageLocation,
  type ThermalReferenceMetadata,
  type ThermalReferenceMetadataInput,
} from "../../api/client";

Icon.add({ add, refresh });

const StyledPageHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
`;

const StyledDialogContent = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
  width: 100%;
`;

const StyledBlobSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
  border: 1px solid #dcdcdc;
  border-radius: 0.5rem;
`;

const StyledActions = styled.div`
  display: flex;
  gap: 0.5rem;
`;

const emptyLocation = (): BlobStorageLocation => ({
  storageAccount: "",
  blobContainer: "",
  blobName: "",
});

const emptyForm = (): ThermalReferenceMetadataInput => ({
  tagId: "",
  installationCode: "",
  inspectionDescription: "",
  referenceBlobStorageDirectoryLocation: emptyLocation(),
});

type ThermalReferenceMetadataDialogProps = {
  open: boolean;
  mode: "create" | "edit";
  initialValue: ThermalReferenceMetadata | null;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
};

function formatBlobLocation(location: BlobStorageLocation) {
  return `${location.storageAccount}/${location.blobContainer}/${location.blobName}`;
}

function BlobLocationFields({
  title,
  location,
  onChange,
}: {
  title: string;
  location: BlobStorageLocation;
  onChange: (location: BlobStorageLocation) => void;
}) {
  return (
    <StyledBlobSection>
      <Typography variant="h6">{title}</Typography>
      <TextField
        id={`${title}-storage-account`}
        label="Storage Account"
        value={location.storageAccount}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
          onChange({ ...location, storageAccount: e.target.value })
        }
      />
      <TextField
        id={`${title}-blob-container`}
        label="Blob Container"
        value={location.blobContainer}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
          onChange({ ...location, blobContainer: e.target.value })
        }
      />
      <TextField
        id={`${title}-blob-name`}
        label="Blob Name"
        value={location.blobName}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
          onChange({ ...location, blobName: e.target.value })
        }
      />
    </StyledBlobSection>
  );
}

function ThermalReferenceMetadataDialog({
  open,
  mode,
  initialValue,
  onClose,
  onSaved,
  onError,
}: ThermalReferenceMetadataDialogProps) {
  const [form, setForm] = useState<ThermalReferenceMetadataInput>(emptyForm);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) {
      return;
    }

    if (initialValue) {
      setForm({
        tagId: initialValue.tagId,
        installationCode: initialValue.installationCode,
        inspectionDescription: initialValue.inspectionDescription,
        referenceBlobStorageDirectoryLocation: emptyLocation(),
      });
      return;
    }

    setForm(emptyForm());
  }, [initialValue, open]);

  const handleSave = async () => {
    setSaving(true);
    try {
      if (mode === "create") {
        await createThermalReferenceMetadata(form);
      } else if (initialValue) {
        await updateThermalReferenceMetadata(initialValue.id, form);
      }

      onClose();
      onSaved();
    } catch (e) {
      onError(
        e instanceof Error
          ? e.message
          : `Failed to ${mode === "create" ? "create" : "update"} thermal reference metadata`
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} style={{ width: "min(40rem, 90vw)" }}>
      <Dialog.Header>
        <Dialog.Title>
          {mode === "create" ? "Create Thermal Reference Metadata" : "Edit Thermal Reference Metadata"}
        </Dialog.Title>
      </Dialog.Header>
      <Dialog.CustomContent>
        <StyledDialogContent>
          <TextField
            id="tag-id"
            label="Tag ID"
            value={form.tagId}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({ ...form, tagId: e.target.value })
            }
          />
          <TextField
            id="installation-code"
            label="Installation Code"
            value={form.installationCode}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({ ...form, installationCode: e.target.value })
            }
          />
          <TextField
            id="inspection-description"
            label="Inspection Description"
            value={form.inspectionDescription}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
              setForm({ ...form, inspectionDescription: e.target.value })
            }
          />

          <BlobLocationFields
            title="Reference Blob Storage Directory"
            location={form.referenceBlobStorageDirectoryLocation}
            onChange={(location) =>
              setForm({ ...form, referenceBlobStorageDirectoryLocation: location })
            }
          />
        </StyledDialogContent>
      </Dialog.CustomContent>
      <Dialog.Actions>
        <Button onClick={handleSave} disabled={saving}>
          {saving ? "Saving..." : mode === "create" ? "Create" : "Save"}
        </Button>
        <Button variant="ghost" onClick={onClose}>
          Cancel
        </Button>
      </Dialog.Actions>
    </Dialog>
  );
}

export default function ThermalReferenceImagesPage() {
  const [data, setData] = useState<ThermalReferenceMetadata[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [selectedMetadata, setSelectedMetadata] =
    useState<ThermalReferenceMetadata | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getThermalReferenceMetadata();
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
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const sortedData = useMemo(
    () =>
      [...data].sort(
        (left, right) =>
          new Date(right.dateCreated).getTime() - new Date(left.dateCreated).getTime()
      ),
    [data]
  );

  const handleDelete = async (id: string) => {
    try {
      await deleteThermalReferenceMetadata(id);
      await fetchData();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to delete thermal reference metadata"
      );
    }
  };

  return (
    <div style={{ paddingTop: "1rem" }}>
      <StyledPageHeader>
        <Typography variant="h3">Thermal Reference Metadata</Typography>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="ghost_icon"
            onClick={fetchData}
            aria-label="Refresh"
            disabled={loading}
          >
            <Icon name="refresh" />
          </Button>
          <Button onClick={() => setShowCreate(true)}>
            <Icon name="add" />
            New Reference Metadata
          </Button>
        </div>
      </StyledPageHeader>

      {error && (
        <Typography
          variant="body_short"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      {loading ? (
        <Typography variant="body_short">Loading...</Typography>
      ) : (
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Installation</Table.Cell>
              <Table.Cell>Tag</Table.Cell>
              <Table.Cell>Inspection Description</Table.Cell>
              <Table.Cell>Reference Image</Table.Cell>
              <Table.Cell>Reference Polygon</Table.Cell>
              <Table.Cell>Actions</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {sortedData.map((metadata) => (
              <Table.Row
                key={metadata.id}
                onClick={() => setSelectedMetadata(metadata)}
                style={{ cursor: "pointer" }}
              >
                <Table.Cell>{metadata.installationCode}</Table.Cell>
                <Table.Cell>{metadata.tagId}</Table.Cell>
                <Table.Cell>{metadata.inspectionDescription}</Table.Cell>
                <Table.Cell>
                  {formatBlobLocation(metadata.referenceImageBlobStorageLocation)}
                </Table.Cell>
                <Table.Cell>
                  {formatBlobLocation(metadata.referencePolygonBlobStorageLocation)}
                </Table.Cell>
                <Table.Cell>
                  <StyledActions>
                    <Button
                      variant="ghost"
                      onClick={(event) => {
                        event.stopPropagation();
                        setSelectedMetadata(metadata);
                      }}
                    >
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      color="danger"
                      onClick={(event) => {
                        event.stopPropagation();
                        handleDelete(metadata.id);
                      }}
                    >
                      Delete
                    </Button>
                  </StyledActions>
                </Table.Cell>
              </Table.Row>
            ))}
            {sortedData.length === 0 && (
              <Table.Row>
                <Table.Cell colSpan={6}>
                  <Typography variant="body_short">
                    No thermal reference metadata found.
                  </Typography>
                </Table.Cell>
              </Table.Row>
            )}
          </Table.Body>
        </Table>
      )}

      <ThermalReferenceMetadataDialog
        open={showCreate}
        mode="create"
        initialValue={null}
        onClose={() => setShowCreate(false)}
        onSaved={fetchData}
        onError={setError}
      />

      <ThermalReferenceMetadataDialog
        open={selectedMetadata !== null}
        mode="edit"
        initialValue={selectedMetadata}
        onClose={() => setSelectedMetadata(null)}
        onSaved={fetchData}
        onError={setError}
      />
    </div>
  );
}