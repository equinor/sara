import { useMemo, useState } from "react";
import { Button, Dialog, Icon, Typography } from "@equinor/eds-core-react";
import { play } from "@equinor/eds-icons";
import type { PlantData, Workflow } from "../api/client";

Icon.add({ play });

interface WorkflowTriggerButtonProps {
  data: PlantData;
  triggering: boolean;
  onTrigger: (plantData: PlantData) => Promise<void> | void;
}

function getWorkflowChain(data: PlantData): Workflow[] {
  return [
    data.anonymization,
    data.cloeAnalysis ?? null,
    data.fencillaAnalysis ?? null,
    data.thermalReadingAnalysis ?? null,
  ].filter((workflow): workflow is Workflow => workflow !== null);
}

export default function WorkflowTriggerButton({
  data,
  triggering,
  onTrigger,
}: WorkflowTriggerButtonProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);

  const actionState = useMemo(() => {
    const workflowChain = getWorkflowChain(data);
    const isRunning = workflowChain.some((workflow) => workflow.status === "Started");
    const hasCompletedRun = workflowChain.some(
      (workflow) =>
        workflow.status === "ExitSuccess" || workflow.status === "ExitFailure"
    );

    return {
      isRunning,
      label: hasCompletedRun ? "Rerun" : "Trigger",
      requiresConfirmation: hasCompletedRun,
    };
  }, [data]);

  const handleClick = async () => {
    if (actionState.requiresConfirmation) {
      setConfirmOpen(true);
      return;
    }

    await onTrigger(data);
  };

  const handleConfirm = async () => {
    setConfirmOpen(false);
    await onTrigger(data);
  };

  return (
    <>
      <Button variant="ghost" onClick={handleClick} disabled={triggering || actionState.isRunning}>
        <Icon name="play" />
        {triggering ? "Triggering..." : actionState.label}
      </Button>
      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)}>
        <Dialog.Header>
          <Dialog.Title>Rerun workflow chain?</Dialog.Title>
        </Dialog.Header>
        <Dialog.CustomContent>
          <Typography variant="body_short">
            This reruns the full workflow chain and overwrites previously produced analysis data.
          </Typography>
        </Dialog.CustomContent>
        <Dialog.Actions>
          <Button onClick={handleConfirm} disabled={triggering}>
            {triggering ? "Triggering..." : "Rerun"}
          </Button>
          <Button variant="ghost" onClick={() => setConfirmOpen(false)} disabled={triggering}>
            Cancel
          </Button>
        </Dialog.Actions>
      </Dialog>
    </>
  );
}