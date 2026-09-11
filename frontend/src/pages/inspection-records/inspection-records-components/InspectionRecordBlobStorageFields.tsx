import { TextField, Typography } from "@equinor/eds-core-react";
import { sectionStyle, type InspectionRecordFormSectionProps } from "./inspectionRecordForm";

export default function InspectionRecordBlobStorageFields({ form, set }: InspectionRecordFormSectionProps) {
  return (
    <>
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
    </>
  );
}
