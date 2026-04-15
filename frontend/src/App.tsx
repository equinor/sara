import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { useEffect, useCallback, useState } from "react";
import { Tabs, Typography, Button, TopBar, Icon } from "@equinor/eds-core-react";
import { code } from "@equinor/eds-icons";
import { getAppConfig, createLoginRequest } from "./authConfig";
import { setMsalInstance } from "./api/client";
import PlantDataPage from "./pages/plant-data";
import AnalysisMappingsPage from "./pages/analysis-mappings";
import CreatePlantDataPage from "./pages/create-plant-data";
import PlantDataDetailPage from "./pages/plant-data-detail";

Icon.add({ code });

const TABS = [
  { path: "/plant-data", label: "Plant Data" },
  { path: "/analysis-mappings", label: "Analysis Mappings" },
];

function getTabFromPath(): number {
  const idx = TABS.findIndex((t) => window.location.pathname === t.path);
  return idx >= 0 ? idx : 0;
}

function App() {
  const { instance } = useMsal();
  const [, setForceRender] = useState(0);

  useEffect(() => {
    setMsalInstance(instance);
  }, [instance]);

  useEffect(() => {
    if (window.location.pathname === "/" && !window.location.hash) {
      window.history.replaceState(null, "", TABS[0].path);
    }
  }, []);

  const activeTab = getTabFromPath();

  const handleTabChange = useCallback((val: string | number) => {
    const idx = Number(val);
    window.history.pushState(null, "", TABS[idx].path);
    // Force re-render after pushState
    window.dispatchEvent(new PopStateEvent("popstate"));
  }, []);

  useEffect(() => {
    const onPopState = () => {
      // Force re-render on back/forward navigation
      setForceRender((n) => n + 1);
    };
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  const handleLogin = () => {
    instance.loginRedirect(createLoginRequest(getAppConfig()));
  };

  return (
    <>
      <TopBar>
        <TopBar.Header>SARA</TopBar.Header>
        <TopBar.Actions>
          <Button
            variant="ghost"
            href="/swagger"
            target="_blank"
            rel="noopener noreferrer"
            as="a"
          >
            <Icon name="code" />
            API Docs
          </Button>
          <AuthenticatedTemplate>
            <Typography variant="body_short">
              {instance.getAllAccounts()[0]?.name ?? ""}
            </Typography>
          </AuthenticatedTemplate>
        </TopBar.Actions>
      </TopBar>

      <UnauthenticatedTemplate>
        <div
          style={{
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            height: "80vh",
          }}
        >
          <Button onClick={handleLogin}>Sign in</Button>
        </div>
      </UnauthenticatedTemplate>

      <AuthenticatedTemplate>
        <div style={{ padding: "1rem" }}>
          {window.location.pathname === "/create-plant-data" ? (
            <CreatePlantDataPage />
          ) : /^\/plant-data\/(.+)$/.exec(window.location.pathname) ? (
            <PlantDataDetailPage
              id={/^\/plant-data\/(.+)$/.exec(window.location.pathname)![1]}
            />
          ) : (
            <Tabs activeTab={activeTab} onChange={handleTabChange}>
              <Tabs.List>
                {TABS.map((t) => (
                  <Tabs.Tab key={t.path}>{t.label}</Tabs.Tab>
                ))}
              </Tabs.List>
              <Tabs.Panels>
                <Tabs.Panel>
                  <PlantDataPage />
                </Tabs.Panel>
                <Tabs.Panel>
                  <AnalysisMappingsPage />
                </Tabs.Panel>
              </Tabs.Panels>
            </Tabs>
          )}
        </div>
      </AuthenticatedTemplate>
    </>
  );
}

export default App;
