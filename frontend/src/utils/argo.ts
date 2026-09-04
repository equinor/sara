import { getAppConfig } from "../authConfig";

/** Build a link to an Argo workflow, if Argo is configured and the workflow exists. */
export function argoWorkflowUrl(
  argoWorkflowName: string | null | undefined
): string | null {
  const { argoWorkflowsBaseUrl, argoWorkflowsNamespace } = getAppConfig();
  if (!argoWorkflowsBaseUrl || !argoWorkflowsNamespace || !argoWorkflowName) {
    return null;
  }
  return `${argoWorkflowsBaseUrl}/workflows/${argoWorkflowsNamespace}/${argoWorkflowName}`;
}

/** Build a link selecting a specific node in an Argo workflow. */
export function argoWorkflowStepUrl(
  argoWorkflowName: string | null | undefined,
  argoNodeId: string | null | undefined,
  argoWorkflowUid: string | null | undefined
): string | null {
  const workflowUrl = argoWorkflowUrl(argoWorkflowName);
  if (!workflowUrl || !argoNodeId || !argoWorkflowUid) return null;

  const params = new URLSearchParams({
    tab: "workflow",
    nodeId: argoNodeId,
    uid: argoWorkflowUid,
  });
  return `${workflowUrl}?${params}`;
}
