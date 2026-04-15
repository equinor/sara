import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  TextField,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import {
  createPlantData,
  getAnalysisMappings,
  type PlantDataRequest,
  type AnalysisMapping,
} from "../../api/client";
import MappingPickerDialog from "./MappingPickerDialog";
import BlobStorageFields from "./BlobStorageFields";

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

  const navigateBack = () => {
    window.history.pushState(null, "", "/plant-data");
    window.dispatchEvent(new PopStateEvent("popstate"));
  };

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
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "1.5rem" }}>
        <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
          <Icon name="arrow_back" />
        </Button>
        <Typography variant="h3">Create Plant Data</Typography>
      </div>

      {error && (
        <Typography
          variant="body_short"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      <div style={{ maxWidth: 640, display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
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
        </div>
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

        <div style={{ display: "flex", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button onClick={handleCreate} disabled={creating}>
            {creating ? "Creating..." : "Create"}
          </Button>
          <Button variant="ghost" onClick={navigateBack}>
            Cancel
          </Button>
        </div>
      </div>

      <MappingPickerDialog
        open={showMappingPicker}
        onClose={() => setShowMappingPicker(false)}
        mappings={mappings}
        onSelect={(update) => setForm({ ...form, ...update })}
      />
    </div>
  );
}
