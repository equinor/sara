import { getAppConfig, createLoginRequest } from "../authConfig";
import { apiUrl } from "../utils/routing";
import type { IPublicClientApplication } from "@azure/msal-browser";

let msalInstance: IPublicClientApplication | null = null;

export function setMsalInstance(instance: IPublicClientApplication) {
  msalInstance = instance;
}

async function getAccessToken(): Promise<string> {
  if (!msalInstance) throw new Error("MSAL not initialized");

  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) throw new Error("No accounts found");

  const response = await msalInstance.acquireTokenSilent({
    ...createLoginRequest(getAppConfig()),
    account: accounts[0],
  });
  return response.accessToken;
}

async function apiFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const token = await getAccessToken();
  const response = await fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status}: ${text}`);
  }
  if (response.status === 204) return undefined as unknown as T;
  const contentType = response.headers.get("content-type");
  if (contentType?.includes("application/json")) {
    return response.json();
  }
  return response.text() as unknown as T;
}

// --- Shared types ---

export interface BlobStorageLocation {
  storageAccount: string;
  blobContainer: string;
  blobName: string;
}

export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export type AnalysisGroupStatus = "Pending" | "Complete" | "TimedOut";
export type AnalysisRunStatus =
  | "Pending"
  | "InProgress"
  | "Succeeded"
  | "Failed";
export type WorkflowStatus = "Pending" | "InProgress" | "Succeeded" | "Failed";

// --- Domain types ---

export interface Position {
  x: number;
  y: number;
  z: number;
}

export interface Orientation {
  x: number;
  y: number;
  z: number;
  w: number;
}

export interface Pose {
  position: Position;
  orientation: Orientation;
}

export interface AnalysisGroupRef {
  analysisGroupId: string;
  analysisGroupSize: number;
  analysisGroupAnalyses: string[];
}

export interface InspectionRecord {
  id: string;
  inspectionId: string;
  installationCode: string;
  blobStorageLocation: BlobStorageLocation;
  createdAt: string;
  inspectionType?: string | null;
  tag?: string | null;
  targetPosition?: Position | null;
  robotPose?: Pose | null;
  inspectionDescription?: string | null;
  robotName?: string | null;
  timestamp?: string | null;
  analysisGroupId?: string | null;
  analyses?: Analysis[];
}

export interface Workflow {
  id: string;
  analysisRunId: string;
  stepNumber: number;
  workflowType: string;
  inputBlobStorageLocations: BlobStorageLocation[];
  status: WorkflowStatus;
  outputBlobStorageLocation?: BlobStorageLocation | null;
  resultJson?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  errorMessage?: string | null;
  analysisRun?: AnalysisRun;
}

export interface AnalysisRun {
  id: string;
  analysisId: string;
  runNumber: number;
  status: AnalysisRunStatus;
  startedAt?: string | null;
  completedAt?: string | null;
  workflows?: Workflow[];
  analysis?: Analysis;
}

export interface Analysis {
  id: string;
  name: string;
  createdAt: string;
  analysisGroupId?: string | null;
  analysisGroup?: AnalysisGroup | null;
  inspectionRecords?: InspectionRecord[];
  runs?: AnalysisRun[];
}

export interface AnalysisGroup {
  id: string;
  groupId: string;
  expectedSize: number;
  status: AnalysisGroupStatus;
  timeoutAt?: string | null;
  inspectionRecords?: InspectionRecord[];
  analyses?: Analysis[];
}

// --- Thermal Reference Metadata (unchanged) ---

export interface ThermalReferenceMetadata {
  id: string;
  tagId: string;
  installationCode: string;
  inspectionDescription: string;
  dateCreated: string;
  referenceImageBlobStorageLocation: BlobStorageLocation;
  referencePolygonBlobStorageLocation: BlobStorageLocation;
}

export interface BlobDirectoryInput {
  blobContainer: string;
  blobName: string;
}

export interface ThermalReferenceMetadataInput {
  tagId: string;
  installationCode: string;
  inspectionDescription: string;
  referenceBlobStorageDirectory: BlobDirectoryInput;
}

// --- Inspection Records ---

export interface InspectionRecordParams {
  inspectionId?: string;
  tag?: string;
  installationCode?: string;
}

function pagedQuery(
  pageNumber: number,
  pageSize: number,
  extra: Record<string, string | undefined> = {}
): string {
  const params = new URLSearchParams({
    PageNumber: String(pageNumber),
    PageSize: String(pageSize),
  });
  for (const [k, v] of Object.entries(extra)) {
    if (v != null && v !== "") params.set(k, v);
  }
  return params.toString();
}

export async function getInspectionRecords(
  pageNumber = 1,
  pageSize = 25,
  filters: InspectionRecordParams = {}
): Promise<PagedResponse<InspectionRecord>> {
  const q = pagedQuery(pageNumber, pageSize, {
    InspectionId: filters.inspectionId,
    Tag: filters.tag,
    InstallationCode: filters.installationCode,
  });
  return apiFetch(apiUrl(`/api/inspection-record?${q}`));
}

export async function getInspectionRecord(id: string): Promise<InspectionRecord> {
  return apiFetch(apiUrl(`/api/inspection-record/id/${encodeURIComponent(id)}`));
}

export interface CreateInspectionRecordRequest {
  inspectionId: string;
  installationCode: string;
  blobStorageLocation: BlobStorageLocation;
  inspectionType?: string;
  tag?: string;
  inspectionDescription?: string;
  robotName?: string;
  timestamp?: string;
  requiredAnalysis?: string[];
  analysisGroup?: AnalysisGroupRef;
}

export async function createInspectionRecord(
  request: CreateInspectionRecordRequest
): Promise<InspectionRecord> {
  return apiFetch(apiUrl("/api/inspection-record"), {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function deleteInspectionRecord(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/inspection-record/id/${encodeURIComponent(id)}`), {
    method: "DELETE",
  });
}

// --- Analyses ---

export interface AnalysisParams {
  name?: string;
  analysisGroupId?: string;
  inspectionRecordId?: string;
}

export async function getAnalyses(
  pageNumber = 1,
  pageSize = 25,
  filters: AnalysisParams = {}
): Promise<PagedResponse<Analysis>> {
  const q = pagedQuery(pageNumber, pageSize, {
    Name: filters.name,
    AnalysisGroupId: filters.analysisGroupId,
    InspectionRecordId: filters.inspectionRecordId,
  });
  return apiFetch(apiUrl(`/api/analysis?${q}`));
}

export async function getAnalysis(id: string): Promise<Analysis> {
  return apiFetch(apiUrl(`/api/analysis/id/${encodeURIComponent(id)}`));
}

export async function rerunAnalysis(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/analysis/id/${encodeURIComponent(id)}/rerun`), {
    method: "POST",
  });
}

export async function deleteAnalysis(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/analysis/id/${encodeURIComponent(id)}`), {
    method: "DELETE",
  });
}

// --- Analysis Groups ---

export interface AnalysisGroupParams {
  groupId?: string;
  status?: AnalysisGroupStatus;
}

export async function getAnalysisGroups(
  pageNumber = 1,
  pageSize = 25,
  filters: AnalysisGroupParams = {}
): Promise<PagedResponse<AnalysisGroup>> {
  const q = pagedQuery(pageNumber, pageSize, {
    GroupId: filters.groupId,
    Status: filters.status,
  });
  return apiFetch(apiUrl(`/api/analysis-group?${q}`));
}

export async function getAnalysisGroup(id: string): Promise<AnalysisGroup> {
  return apiFetch(apiUrl(`/api/analysis-group/id/${encodeURIComponent(id)}`));
}

export async function deleteAnalysisGroup(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/analysis-group/id/${encodeURIComponent(id)}`), {
    method: "DELETE",
  });
}

// --- Analysis Runs ---

export interface AnalysisRunParams {
  analysisId?: string;
  status?: AnalysisRunStatus;
}

export async function getAnalysisRuns(
  pageNumber = 1,
  pageSize = 25,
  filters: AnalysisRunParams = {}
): Promise<PagedResponse<AnalysisRun>> {
  const q = pagedQuery(pageNumber, pageSize, {
    AnalysisId: filters.analysisId,
    Status: filters.status,
  });
  return apiFetch(apiUrl(`/api/analysis-run?${q}`));
}

export async function getAnalysisRun(id: string): Promise<AnalysisRun> {
  return apiFetch(apiUrl(`/api/analysis-run/id/${encodeURIComponent(id)}`));
}

export async function deleteAnalysisRun(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/analysis-run/id/${encodeURIComponent(id)}`), {
    method: "DELETE",
  });
}

// --- Workflows ---

export interface WorkflowParams {
  workflowType?: string;
  status?: WorkflowStatus;
  analysisRunId?: string;
}

export async function getWorkflows(
  pageNumber = 1,
  pageSize = 25,
  filters: WorkflowParams = {}
): Promise<PagedResponse<Workflow>> {
  const q = pagedQuery(pageNumber, pageSize, {
    WorkflowType: filters.workflowType,
    Status: filters.status,
    AnalysisRunId: filters.analysisRunId,
  });
  return apiFetch(apiUrl(`/api/workflow?${q}`));
}

export async function getWorkflow(id: string): Promise<Workflow> {
  return apiFetch(apiUrl(`/api/workflow/id/${encodeURIComponent(id)}`));
}

export async function retryWorkflow(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/workflow/id/${encodeURIComponent(id)}/retry`), {
    method: "POST",
  });
}

export async function deleteWorkflow(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/workflow/id/${encodeURIComponent(id)}`), {
    method: "DELETE",
  });
}

// --- Config: configured analysis names ---

export interface AnalysisConfigEntry {
  name: string;
  workflows: string[];
}

export async function getConfiguredAnalyses(): Promise<AnalysisConfigEntry[]> {
  return apiFetch(apiUrl("/api/config/analyses"));
}

// --- Thermal Reference Metadata ---

export async function getThermalReferenceMetadata(): Promise<ThermalReferenceMetadata[]> {
  return apiFetch(apiUrl(`/api/ThermalReferenceMetadata`));
}

export async function getThermalReferenceMetadataById(
  id: string
): Promise<ThermalReferenceMetadata> {
  return apiFetch(apiUrl(`/api/ThermalReferenceMetadata/id/${encodeURIComponent(id)}`));
}

export async function createThermalReferenceMetadata(
  request: ThermalReferenceMetadataInput
): Promise<ThermalReferenceMetadata> {
  return apiFetch(apiUrl("/api/ThermalReferenceMetadata"), {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function updateThermalReferenceMetadata(
  id: string,
  request: ThermalReferenceMetadataInput
): Promise<ThermalReferenceMetadata> {
  return apiFetch(
    apiUrl(`/api/ThermalReferenceMetadata/id/${encodeURIComponent(id)}`),
    {
      method: "PUT",
      body: JSON.stringify(request),
    }
  );
}

export async function deleteThermalReferenceMetadata(id: string): Promise<void> {
  await apiFetch(apiUrl(`/api/ThermalReferenceMetadata/id/${encodeURIComponent(id)}`), {
    method: "DELETE",
  });
}
