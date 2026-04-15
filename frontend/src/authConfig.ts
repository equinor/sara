import { Configuration, LogLevel } from "@azure/msal-browser";

export interface AppConfig {
  clientId: string;
  tenantId: string;
  basePath: string;
}

let appConfig: AppConfig = { clientId: "", tenantId: "", basePath: "" };

export function getAppConfig(): AppConfig {
  return appConfig;
}

export async function loadAppConfig(): Promise<AppConfig> {
  try {
    const res = await fetch("api/config");
    if (res.ok) {
      const data = await res.json();
      appConfig = {
        clientId: data.azureAd?.clientId ?? "",
        tenantId: data.azureAd?.tenantId ?? "",
        basePath: data.basePath ?? "",
      };
    }
  } catch {
    // Fallback to Vite env vars for local dev without backend
    appConfig = {
      clientId: import.meta.env.VITE_AZURE_AD_CLIENT_ID ?? "",
      tenantId: import.meta.env.VITE_AZURE_AD_TENANT_ID ?? "",
      basePath: "",
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
