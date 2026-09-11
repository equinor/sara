import type { CSSProperties } from "react";

export interface InspectionRecordForm {
  inspectionId: string;
  installationCode: string;
  tag: string;
  inspectionDescription: string;
  inspectionType: string;
  robotName: string;
  storageAccount: string;
  blobContainer: string;
  blobName: string;
  useGroup: boolean;
  groupId: string;
  groupSize: number;
  groupAnalyses: string;
}

export interface InspectionRecordFormSectionProps {
  form: InspectionRecordForm;
  set: <K extends keyof InspectionRecordForm>(key: K, value: InspectionRecordForm[K]) => void;
}

export const sectionStyle: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "1fr 1fr",
  gap: "0.75rem",
  marginBottom: "1.5rem",
};
