import { Button, Typography, Icon } from "@equinor/eds-core-react";
import { play } from "@equinor/eds-icons";
import type { PlantData } from "../../api/client";
import WorkflowSection from "./WorkflowSection";

Icon.add({ play });

interface AnonymizationSectionProps {
  data: PlantData;
  triggering: boolean;
  onTrigger: () => void;
}

export default function AnonymizationSection({ data, triggering, onTrigger }: AnonymizationSectionProps) {
  const canTrigger =
    data.anonymization.status !== "Started" &&
    data.anonymization.status !== "ExitSuccess";

  return (
    <>
      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "0.75rem" }}>
        <Typography variant="h5">Anonymization</Typography>
        {canTrigger && (
          <Button
            variant="ghost"
            onClick={onTrigger}
            disabled={triggering}
          >
            <Icon name="play" />
            {triggering ? "Triggering..." : "Trigger Anonymizer"}
          </Button>
        )}
      </div>
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
