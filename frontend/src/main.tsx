import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import { BrowserRouter } from "react-router";
import { loadAppConfig, createMsalConfig } from "./authConfig";
import App from "./App";
import { setMsalInstance } from "./api/client";

loadAppConfig().then((config) => {
  const msalInstance = new PublicClientApplication(createMsalConfig(config));
  setMsalInstance(msalInstance);

  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <BrowserRouter basename={config.basePath || "/"}>
        <MsalProvider instance={msalInstance}>
          <App />
        </MsalProvider>
      </BrowserRouter>
    </StrictMode>
  );
});
