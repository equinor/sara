import { Configuration, LogLevel } from "@azure/msal-browser";

export interface AppConfig {
  clientId: string;
  tenantId: string;
  basePath: string;
  flotillaBaseUrl: string;
  argoWorkflowsBaseUrl: string;
  argoWorkflowsNamespace: string;
}

let appConfig: AppConfig = {
  clientId: "",
  tenantId: "",
  basePath: "",
  flotillaBaseUrl: "",
  argoWorkflowsBaseUrl: "",
  argoWorkflowsNamespace: "",
};

export function getAppConfig(): AppConfig {
  return appConfig;
}

/**
 * Build a deep link to the Argo Workflows UI for a given Argo workflow name.
 * Returns null when the base URL is not configured (e.g. local dev) or the
 * workflow has no stored Argo name (older records / not yet started).
 */
export function argoWorkflowUrl(
  argoWorkflowName: string | null | undefined,
): string | null {
  const { argoWorkflowsBaseUrl, argoWorkflowsNamespace } = appConfig;
  if (!argoWorkflowsBaseUrl || !argoWorkflowsNamespace || !argoWorkflowName) {
    return null;
  }
  return `${argoWorkflowsBaseUrl}/workflows/${argoWorkflowsNamespace}/${argoWorkflowName}`;
}

// Resolve the config endpoint against the app's base (where the bundle lives),
// not the current route. With Vite's relative base the bundle is served from
// `{basePath}/assets/*.js`, so "../api/config" always resolves to
// `{basePath}/api/config` in dev and any deployed sub-path. Using a route-relative
// URL instead breaks on deep links/refreshes (e.g. /inspection-records/:id).
const configUrl = new URL(/* @vite-ignore */ "../api/config", import.meta.url);

export async function loadAppConfig(): Promise<AppConfig> {
  try {
    const res = await fetch(configUrl);
    if (res.ok) {
      const data = await res.json();
      appConfig = {
        clientId: data.azureAd?.clientId ?? "",
        tenantId: data.azureAd?.tenantId ?? "",
        basePath: data.basePath ?? "",
        flotillaBaseUrl: data.flotillaBaseUrl ?? "",
        argoWorkflowsBaseUrl: data.argoWorkflowsBaseUrl ?? "",
        argoWorkflowsNamespace: data.argoWorkflowsNamespace ?? "",
      };
    }
  } catch {
    // Fallback to Vite env vars for local dev without backend
    appConfig = {
      clientId: import.meta.env.VITE_AZURE_AD_CLIENT_ID ?? "",
      tenantId: import.meta.env.VITE_AZURE_AD_TENANT_ID ?? "",
      basePath: "",
      flotillaBaseUrl: "",
      argoWorkflowsBaseUrl: "",
      argoWorkflowsNamespace: "",
    };
  }
  return appConfig;
}

export function createMsalConfig(config: AppConfig): Configuration {
  return {
    auth: {
      clientId: config.clientId,
      authority: `https://login.microsoftonline.com/${config.tenantId}`,
      redirectUri: window.location.origin + (config.basePath || "") + "/",
    },
    cache: {
      cacheLocation: "sessionStorage",
    },
    system: {
      loggerOptions: {
        logLevel: LogLevel.Warning,
      },
    },
  };
}

export function createLoginRequest(config: AppConfig) {
  return {
    scopes: [`api://${config.clientId}/user_impersonation`],
  };
}
