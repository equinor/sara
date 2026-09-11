import { useState } from "react";
import { useNavigate } from "react-router";
import {
  Button,
  Icon,
  Typography,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import {
  createInspectionRecord,
  type CreateInspectionRecordRequest,
} from "../../api/client";
import { useConfiguredAnalyses, useResourceMutation } from "../../api/queries";
import { ErrorText } from "../../components/Styles";
import InspectionRecordIdentificationFields from "./inspection-records-components/InspectionRecordIdentificationFields";
import InspectionRecordBlobStorageFields from "./inspection-records-components/InspectionRecordBlobStorageFields";
import InspectionRecordAnalysisSelection from "./inspection-records-components/InspectionRecordAnalysisSelection";
import InspectionRecordAnalysisGroupFields from "./inspection-records-components/InspectionRecordAnalysisGroupFields";
import type { InspectionRecordForm } from "./inspection-records-components/inspectionRecordForm";

Icon.add({ arrow_back });

const STORAGE_ACCOUNT_DEFAULT = "";

export default function CreateInspectionRecordPage() {
  const navigate = useNavigate();
  const createMutation = useResourceMutation(createInspectionRecord);
  const configuredAnalyses = useConfiguredAnalyses();
  const submitting = createMutation.isPending;
  const [error, setError] = useState<string | null>(null);
  const configured = configuredAnalyses.data ?? [];

  const [form, setForm] = useState<InspectionRecordForm>({
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
    if (submitting) return;
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
      const created = await createMutation.mutateAsync(req);
      navigate(`/inspection-records/${created.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed");
    }
  };

  return (
    <div style={{ paddingTop: "1rem", maxWidth: "900px" }}>
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0 1.5rem" }}>
        New Inspection Record
      </Typography>

      <InspectionRecordIdentificationFields form={form} set={set} />
      <InspectionRecordBlobStorageFields form={form} set={set} />
      <InspectionRecordAnalysisSelection
        configured={configured}
        isPending={configuredAnalyses.isPending}
        error={configuredAnalyses.error}
        selectedAnalyses={selectedAnalyses}
        toggleAnalysis={toggleAnalysis}
      />
      <InspectionRecordAnalysisGroupFields form={form} set={set} />

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
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
