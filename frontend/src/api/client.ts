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

async function apiFetch<T>(
  url: string,
  options: RequestInit = {}
): Promise<T> {
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
  const contentType = response.headers.get("content-type");
  if (contentType?.includes("application/json")) {
    return response.json();
  }
  return response.text() as unknown as T;
}

// --- Types ---

export interface BlobStorageLocation {
  storageAccount: string;
  blobContainer: string;
  blobName: string;
}

export interface Workflow {
  id: string;
  sourceBlobStorageLocation: BlobStorageLocation;
  destinationBlobStorageLocation: BlobStorageLocation;
  dateCreated: string;
  status: "NotStarted" | "Started" | "ExitSuccess" | "ExitFailure";
}

export interface Anonymization extends Workflow {
  isPersonInImage?: boolean | null;
  preProcessedBlobStorageLocation?: BlobStorageLocation | null;
}

export interface CLOEAnalysis extends Workflow {
  oilLevel?: number | null;
  confidence?: number | null;
}

export interface FencillaAnalysis extends Workflow {
  isBreak?: boolean | null;
  confidence?: number | null;
}

export interface ThermalReadingAnalysis extends Workflow {
  temperature?: number | null;
}

export interface PlantData {
  id: string;
  inspectionId: string;
  installationCode: string;
  dateCreated: string;
  tag?: string | null;
  coordinates?: string | null;
  inspectionDescription?: string | null;
  robotName?: string | null;
  timestamp?: string | null;
  anonymization: Anonymization;
  cloeAnalysis?: CLOEAnalysis | null;
  fencillaAnalysis?: FencillaAnalysis | null;
  thermalReadingAnalysis?: ThermalReadingAnalysis | null;
}

export interface AnalysisMapping {
  id: string;
  tag: string;
  inspectionDescription: string;
  analysesToBeRun: AnalysisType[];
}

export type AnalysisType =
  | "ConstantLevelOiler"
  | "Fencilla"
  | "ThermalReading";

export interface PlantDataRequest {
  inspectionId: string;
  installationCode: string;
  tagId: string;
  inspectionDescription: string;
  rawDataBlobStorageLocation: BlobStorageLocation;
}

// --- API calls ---

export async function getPlantData(
  pageNumber = 1,
  pageSize = 100
): Promise<PlantData[]> {
  return apiFetch<PlantData[]>(
    apiUrl(`/api/PlantData?PageNumber=${pageNumber}&PageSize=${pageSize}`)
  );
}

export async function getPlantDataById(id: string): Promise<PlantData> {
  return apiFetch<PlantData>(
    apiUrl(`/api/PlantData/id/${encodeURIComponent(id)}`)
  );
}

export async function createPlantData(
  request: PlantDataRequest
): Promise<PlantData> {
  return apiFetch<PlantData>(apiUrl("/api/PlantData"), {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function triggerAnonymizer(plantDataId: string): Promise<string> {
  return apiFetch<string>(
    apiUrl(`/api/TriggerAnalysis/trigger-anonymizer/${encodeURIComponent(plantDataId)}`),
    { method: "POST" }
  );
}

export async function getAnalysisMappings(
  pageNumber = 1,
  pageSize = 100
): Promise<AnalysisMapping[]> {
  return apiFetch<AnalysisMapping[]>(
    apiUrl(`/api/AnalysisMapping?PageNumber=${pageNumber}&PageSize=${pageSize}`)
  );
}

export async function createAnalysisMapping(
  tagId: string,
  inspectionDescription: string,
  analysisType: AnalysisType
): Promise<AnalysisMapping> {
  return apiFetch<AnalysisMapping>(
    apiUrl(`/api/AnalysisMapping/tag/${encodeURIComponent(tagId)}/inspection/${encodeURIComponent(inspectionDescription)}/analysisType/${encodeURIComponent(analysisType)}`),
    { method: "POST" }
  );
}

export async function deleteAnalysisMapping(id: string): Promise<void> {
  await apiFetch<void>(
    apiUrl(`/api/AnalysisMapping/analysisMappingId/${encodeURIComponent(id)}`),
    { method: "DELETE" }
  );
}
