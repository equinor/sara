import { Checkbox, TextField, Typography } from "@equinor/eds-core-react";
import { sectionStyle, type InspectionRecordFormSectionProps } from "./inspectionRecordForm";

export default function InspectionRecordAnalysisGroupFields({ form, set }: InspectionRecordFormSectionProps) {
  return (
    <>
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
    </>
  );
}
