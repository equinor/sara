import { Typography } from "@equinor/eds-core-react";
import type { PlantData } from "../../api/client";
import WorkflowTriggerButton from "../../components/WorkflowTriggerButton";
import WorkflowSection from "./WorkflowSection";
import styled from "styled-components";

const StyledSectionHeader = styled.div`
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
`;

interface AnonymizationSectionProps {
  data: PlantData;
  triggering: boolean;
  onTrigger: (plantData: PlantData) => Promise<void> | void;
}

export default function AnonymizationSection({ data, triggering, onTrigger }: AnonymizationSectionProps) {
  return (
    <>
      <StyledSectionHeader>
        <Typography variant="h5">Anonymization</Typography>
        <WorkflowTriggerButton data={data} triggering={triggering} onTrigger={onTrigger} />
      </StyledSectionHeader>
      <WorkflowSection
        title=""
        workflow={data.anonymization}
        extraFields={[
          {
            label: "Person in Image",
            value: data.anonymization.isPersonInImage == null
              ? "-"
              : data.anonymization.isPersonInImage
                ? "Yes"
                : "No",
          },
          ...(data.anonymization.preProcessedBlobStorageLocation
            ? [{
                label: "Pre-processed Location",
                value: `${data.anonymization.preProcessedBlobStorageLocation.storageAccount}/${data.anonymization.preProcessedBlobStorageLocation.blobContainer}/${data.anonymization.preProcessedBlobStorageLocation.blobName}`,
              }]
            : []),
        ]}
      />
    </>
  );
}
