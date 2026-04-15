import { getAppConfig } from "../authConfig";

/**
 * Prepend the basePath to an API/external path for fetch calls and external links.
 * e.g. apiUrl("/api/PlantData") → "/sara-dev-backend/api/PlantData"
 */
export function apiUrl(path: string): string {
  return `${getAppConfig().basePath}${path}`;
}
