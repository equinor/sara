import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { useEffect } from "react";
import { Routes, Route, Navigate, useNavigate, useLocation } from "react-router";
import { Tabs, Typography, Button, TopBar, Icon } from "@equinor/eds-core-react";
import { code } from "@equinor/eds-icons";
import { getAppConfig, createLoginRequest } from "./authConfig";
import { setMsalInstance } from "./api/client";
import { apiUrl } from "./utils/routing";
import PlantDataPage from "./pages/plant-data";
import AnalysisMappingsPage from "./pages/analysis-mappings";
import CreatePlantDataPage from "./pages/create-plant-data";
import PlantDataDetailPage from "./pages/plant-data-detail";

Icon.add({ code });

const TABS = [
  { path: "/plant-data", label: "Plant Data" },
  { path: "/analysis-mappings", label: "Analysis Mappings" },
];

function App() {
  const { instance } = useMsal();
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    setMsalInstance(instance);
  }, [instance]);

  const activeTab = TABS.findIndex((t) => location.pathname.startsWith(t.path));
  const tabIndex = activeTab >= 0 ? activeTab : 0;

  const handleTabChange = (val: string | number) => {
    navigate(TABS[Number(val)].path);
  };

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
            href={apiUrl("/swagger")}
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
          <Routes>
            <Route index element={<Navigate to="/plant-data" replace />} />
            <Route
              path="/plant-data"
              element={
                <TabbedLayout activeTab={tabIndex} onChange={handleTabChange}>
                  <PlantDataPage />
                </TabbedLayout>
              }
            />
            <Route
              path="/analysis-mappings"
              element={
                <TabbedLayout activeTab={tabIndex} onChange={handleTabChange}>
                  <AnalysisMappingsPage />
                </TabbedLayout>
              }
            />
            <Route path="/create-plant-data" element={<CreatePlantDataPage />} />
            <Route path="/plant-data/:id" element={<PlantDataDetailPage />} />
          </Routes>
        </div>
      </AuthenticatedTemplate>
    </>
  );
}

function TabbedLayout({
  activeTab,
  onChange,
  children,
}: {
  activeTab: number;
  onChange: (val: string | number) => void;
  children: React.ReactNode;
}) {
  return (
    <Tabs activeTab={activeTab} onChange={onChange}>
      <Tabs.List>
        {TABS.map((t) => (
          <Tabs.Tab key={t.path}>{t.label}</Tabs.Tab>
        ))}
      </Tabs.List>
      <Tabs.Panels>
        <Tabs.Panel>{activeTab === 0 ? children : null}</Tabs.Panel>
        <Tabs.Panel>{activeTab === 1 ? children : null}</Tabs.Panel>
      </Tabs.Panels>
    </Tabs>
  );
}

export default App;
