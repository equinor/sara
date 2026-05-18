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
import InspectionRecordsPage from "./pages/inspection-records";
import CreateInspectionRecordPage from "./pages/inspection-records/create";
import InspectionRecordDetailPage from "./pages/inspection-records/detail";
import AnalysesPage from "./pages/analyses";
import AnalysisDetailPage from "./pages/analyses/detail";
import AnalysisGroupsPage from "./pages/analysis-groups";
import AnalysisGroupDetailPage from "./pages/analysis-groups/detail";
import AnalysisRunsPage from "./pages/analysis-runs";
import AnalysisRunDetailPage from "./pages/analysis-runs/detail";
import WorkflowsPage from "./pages/workflows";
import WorkflowDetailPage from "./pages/workflows/detail";
import ThermalReferenceImagesPage from "./pages/thermal-reference-images";
import CreateThermalReferenceMetadataPage from "./pages/thermal-reference-images/create";
import ThermalReferenceMetadataDetailPage from "./pages/thermal-reference-images/detail";
import styled from "styled-components";

const StyledSignInContainer = styled.div`
  display: flex;
  justify-content: center;
  align-items: center;
  height: 80vh;
`;

Icon.add({ code });

const TABS = [
  { path: "/inspection-records", label: "Inspection Records" },
  { path: "/analyses", label: "Analyses" },
  { path: "/analysis-groups", label: "Analysis Groups" },
  { path: "/analysis-runs", label: "Analysis Runs" },
  { path: "/workflows", label: "Workflows" },
  { path: "/thermal-reference-images", label: "Thermal Reference Images" },
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

  const tabbed = (node: React.ReactNode) => (
    <TabbedLayout activeTab={tabIndex} onChange={handleTabChange}>
      {node}
    </TabbedLayout>
  );

  return (
    <>
      <TopBar>
        <TopBar.Header>
          <span
            role="link"
            tabIndex={0}
            onClick={() => navigate("/inspection-records")}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") navigate("/inspection-records");
            }}
            style={{ cursor: "pointer" }}
          >
            SARA
          </span>
        </TopBar.Header>
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
        <StyledSignInContainer>
          <Button onClick={handleLogin}>Sign in</Button>
        </StyledSignInContainer>
      </UnauthenticatedTemplate>

      <AuthenticatedTemplate>
        <div style={{ padding: "1rem" }}>
          <Routes>
            <Route index element={<Navigate to="/inspection-records" replace />} />

            <Route path="/inspection-records" element={tabbed(<InspectionRecordsPage />)} />
            <Route path="/inspection-records/new" element={<CreateInspectionRecordPage />} />
            <Route path="/inspection-records/:id" element={<InspectionRecordDetailPage />} />

            <Route path="/analyses" element={tabbed(<AnalysesPage />)} />
            <Route path="/analyses/:id" element={<AnalysisDetailPage />} />

            <Route path="/analysis-groups" element={tabbed(<AnalysisGroupsPage />)} />
            <Route path="/analysis-groups/:id" element={<AnalysisGroupDetailPage />} />

            <Route path="/analysis-runs" element={tabbed(<AnalysisRunsPage />)} />
            <Route path="/analysis-runs/:id" element={<AnalysisRunDetailPage />} />

            <Route path="/workflows" element={tabbed(<WorkflowsPage />)} />
            <Route path="/workflows/:id" element={<WorkflowDetailPage />} />

            <Route
              path="/thermal-reference-images"
              element={tabbed(<ThermalReferenceImagesPage />)}
            />
            <Route
              path="/thermal-reference-images/new"
              element={<CreateThermalReferenceMetadataPage />}
            />
            <Route
              path="/thermal-reference-images/:id"
              element={<ThermalReferenceMetadataDetailPage />}
            />
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
        {TABS.map((t, i) => (
          <Tabs.Panel key={t.path}>{activeTab === i ? children : null}</Tabs.Panel>
        ))}
      </Tabs.Panels>
    </Tabs>
  );
}

export default App;
