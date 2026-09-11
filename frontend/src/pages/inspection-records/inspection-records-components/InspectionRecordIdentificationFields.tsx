import { TextField, Typography } from "@equinor/eds-core-react";
import { sectionStyle, type InspectionRecordFormSectionProps } from "./inspectionRecordForm";

export default function InspectionRecordIdentificationFields({ form, set }: InspectionRecordFormSectionProps) {
  return (
    <>
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
    </>
  );
}
