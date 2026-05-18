import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import {
  Button,
  Checkbox,
  Icon,
  TextField,
  Typography,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import {
  createInspectionRecord,
  getConfiguredAnalyses,
  type AnalysisConfigEntry,
  type CreateInspectionRecordRequest,
} from "../../api/client";

Icon.add({ arrow_back });

const STORAGE_ACCOUNT_DEFAULT = "";

export default function CreateInspectionRecordPage() {
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [configured, setConfigured] = useState<AnalysisConfigEntry[]>([]);

  const [form, setForm] = useState({
    inspectionId: "",
    installationCode: "",
    tag: "",
    inspectionDescription: "",
    inspectionType: "image",
    robotName: "",
    storageAccount: STORAGE_ACCOUNT_DEFAULT,
    blobContainer: "",
    blobName: "",
    useGroup: false,
    groupId: "",
    groupSize: 1,
    groupAnalyses: "",
  });
  const [selectedAnalyses, setSelectedAnalyses] = useState<Set<string>>(new Set());

  useEffect(() => {
    getConfiguredAnalyses()
      .then(setConfigured)
      .catch((e) =>
        setError(e instanceof Error ? e.message : "Failed to load configured analyses")
      );
  }, []);

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }));

  const toggleAnalysis = (name: string) =>
    setSelectedAnalyses((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });

  const handleSubmit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const req: CreateInspectionRecordRequest = {
        inspectionId: form.inspectionId.trim(),
        installationCode: form.installationCode.trim(),
        blobStorageLocation: {
          storageAccount: form.storageAccount.trim(),
          blobContainer: form.blobContainer.trim(),
          blobName: form.blobName.trim(),
        },
        tag: form.tag.trim() || undefined,
        inspectionDescription: form.inspectionDescription.trim() || undefined,
        inspectionType: form.inspectionType.trim() || undefined,
        robotName: form.robotName.trim() || undefined,
        timestamp: new Date().toISOString(),
        requiredAnalysis:
          selectedAnalyses.size > 0 ? Array.from(selectedAnalyses) : undefined,
      };
      if (form.useGroup) {
        req.analysisGroup = {
          analysisGroupId: form.groupId.trim(),
          analysisGroupSize: Number(form.groupSize),
          analysisGroupAnalyses: form.groupAnalyses
            .split(",")
            .map((s) => s.trim())
            .filter(Boolean),
        };
      }
      const created = await createInspectionRecord(req);
      navigate(`/inspection-records/${created.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed");
    } finally {
      setSubmitting(false);
    }
  };

  const sectionStyle: React.CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: "0.75rem",
    marginBottom: "1.5rem",
  };

  return (
    <div style={{ paddingTop: "1rem", maxWidth: "900px" }}>
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0 1.5rem" }}>
        New Inspection Record
      </Typography>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Identification
      </Typography>
      <div style={sectionStyle}>
        <TextField
          id="inspectionId"
          label="Inspection ID *"
          value={form.inspectionId}
          onChange={(e: any) => set("inspectionId", e.target.value)}
        />
        <TextField
          id="installationCode"
          label="Installation Code *"
          value={form.installationCode}
          onChange={(e: any) => set("installationCode", e.target.value)}
        />
        <TextField
          id="tag"
          label="Tag"
          value={form.tag}
          onChange={(e: any) => set("tag", e.target.value)}
        />
        <TextField
          id="inspectionType"
          label="Inspection Type"
          value={form.inspectionType}
          onChange={(e: any) => set("inspectionType", e.target.value)}
        />
        <TextField
          id="robotName"
          label="Robot Name"
          value={form.robotName}
          onChange={(e: any) => set("robotName", e.target.value)}
        />
        <TextField
          id="inspectionDescription"
          label="Inspection Description"
          value={form.inspectionDescription}
          onChange={(e: any) => set("inspectionDescription", e.target.value)}
        />
      </div>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Blob Storage Location *
      </Typography>
      <div style={sectionStyle}>
        <TextField
          id="storageAccount"
          label="Storage Account"
          value={form.storageAccount}
          onChange={(e: any) => set("storageAccount", e.target.value)}
        />
        <TextField
          id="blobContainer"
          label="Container"
          value={form.blobContainer}
          onChange={(e: any) => set("blobContainer", e.target.value)}
        />
        <TextField
          id="blobName"
          label="Blob Name (e.g. path/to/file.png)"
          value={form.blobName}
          onChange={(e: any) => set("blobName", e.target.value)}
          style={{ gridColumn: "1 / -1" }}
        />
      </div>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Analyses to Run
      </Typography>
      <Typography variant="body_short" style={{ marginBottom: "0.5rem", color: "#666" }}>
        Leave all unchecked to use file-extension defaults.
      </Typography>
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "0.5rem 1.25rem",
          marginBottom: "1.5rem",
        }}
      >
        {configured.length === 0 ? (
          <Typography variant="body_short">No configured analyses found.</Typography>
        ) : (
          configured.map((entry) => (
            <Checkbox
              key={entry.name}
              label={`${entry.name} (${entry.workflows.join(" → ") || "no workflows"})`}
              checked={selectedAnalyses.has(entry.name)}
              onChange={() => toggleAnalysis(entry.name)}
            />
          ))
        )}
      </div>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Analysis Group (optional)
      </Typography>
      <Checkbox
        label="Attach to an analysis group"
        checked={form.useGroup}
        onChange={(e: any) => set("useGroup", e.target.checked)}
      />
      {form.useGroup && (
        <div style={sectionStyle}>
          <TextField
            id="groupId"
            label="Group ID"
            value={form.groupId}
            onChange={(e: any) => set("groupId", e.target.value)}
          />
          <TextField
            id="groupSize"
            label="Expected Size"
            type="number"
            value={String(form.groupSize)}
            onChange={(e: any) => set("groupSize", Number(e.target.value))}
          />
          <TextField
            id="groupAnalyses"
            label="Grouped analyses (comma-separated)"
            value={form.groupAnalyses}
            onChange={(e: any) => set("groupAnalyses", e.target.value)}
            style={{ gridColumn: "1 / -1" }}
          />
        </div>
      )}

      {error && (
        <Typography variant="body_short" style={{ color: "#eb0000", marginBottom: "1rem" }}>
          {error}
        </Typography>
      )}

      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button onClick={handleSubmit} disabled={submitting}>
          {submitting ? "Creating..." : "Create & Trigger Analyses"}
        </Button>
        <Button variant="ghost" onClick={() => navigate(-1)} disabled={submitting}>
          Cancel
        </Button>
      </div>
    </div>
  );
}
