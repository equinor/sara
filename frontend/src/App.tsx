import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useAccount,
  useMsal,
} from "@azure/msal-react";
import { Routes, Route, Navigate, useNavigate, useLocation } from "react-router";
import { Tabs, Typography, Button, TopBar, Icon } from "@equinor/eds-core-react";
import { code } from "@equinor/eds-icons";
import { getAppConfig, createLoginRequest, hasAdminRole } from "./authConfig";
import AuthenticatedQueryProvider from "./components/AuthenticatedQueryProvider";
import { apiUrl } from "./utils/routing";
import OverviewPage from "./pages/overview";
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
import FeedbackPage from "./pages/feedback";
import ReferencePolygonImagesPage from "./pages/reference-images";
import CreateReferencePolygonMetadataPage from "./pages/reference-images/create";
import ReferencePolygonMetadataDetailPage from "./pages/reference-images/detail";
import BenchmarksPage from "./pages/benchmarks";
import BenchmarkDetailPage from "./pages/benchmarks/detail";
import BenchmarkImageDetailPage from "./pages/benchmarks/image-detail";
import styled from "styled-components";

const StyledSignInContainer = styled.div`
  display: flex;
  justify-content: center;
  align-items: center;
  height: 80vh;
`;

Icon.add({ code });

interface TabDefinition {
  path: string;
  label: string;
  adminOnly?: boolean;
}

const TABS: TabDefinition[] = [
  { path: "/overview", label: "Overview" },
  { path: "/inspection-records", label: "Inspection Records" },
  { path: "/analyses", label: "Analyses" },
  { path: "/analysis-groups", label: "Analysis Groups" },
  { path: "/analysis-runs", label: "Analysis Runs" },
  { path: "/workflows", label: "Workflows" },
  { path: "/feedback", label: "Analysis Feedback" },
  { path: "/reference-images", label: "Reference Images" },
  { path: "/benchmarks", label: "Benchmarks", adminOnly: true },
];

function App() {
  const { instance, accounts } = useMsal();
  const activeAccount = useAccount();
  const account = activeAccount ?? accounts[0];
  const isAdmin = hasAdminRole(account);
  const navigate = useNavigate();
  const location = useLocation();

  const visibleTabs = isAdmin ? TABS : TABS.filter((t) => !t.adminOnly);
  const activeTab = visibleTabs.findIndex((t) => location.pathname.startsWith(t.path));
  const tabIndex = activeTab >= 0 ? activeTab : 0;

  const handleTabChange = (val: string | number) => {
    const nextTab = visibleTabs[Number(val)];
    if (nextTab) navigate(nextTab.path);
  };

  const handleLogin = () => {
    instance.loginRedirect(createLoginRequest(getAppConfig()));
  };

  const tabbed = (node: React.ReactNode) => (
    <TabbedLayout tabs={visibleTabs} activeTab={tabIndex} onChange={handleTabChange}>
      {node}
    </TabbedLayout>
  );

  const requireAdmin = (node: React.ReactNode) =>
    isAdmin ? node : <Navigate to="/overview" replace />;

  return (
    <>
      <TopBar>
        <TopBar.Header>
          <span
            role="link"
            tabIndex={0}
            onClick={() => navigate("/overview")}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") navigate("/overview");
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
        <AuthenticatedQueryProvider>
          <div style={{ padding: "1rem" }}>
            <Routes>
              <Route index element={tabbed(<OverviewPage />)} />

              <Route path="/overview" element={tabbed(<OverviewPage />)} />

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

              <Route path="/feedback" element={tabbed(<FeedbackPage />)} />

              <Route
                path="/reference-images"
                element={tabbed(<ReferencePolygonImagesPage />)}
              />
              <Route
                path="/reference-images/new"
                element={<CreateReferencePolygonMetadataPage />}
              />
              <Route
                path="/reference-images/:id"
                element={<ReferencePolygonMetadataDetailPage />}
              />

              <Route path="/benchmarks" element={requireAdmin(tabbed(<BenchmarksPage />))} />
              <Route
                path="/benchmarks/:datasetId/:version/:split/:runId"
                element={requireAdmin(<BenchmarkDetailPage />)}
              />
              <Route
                path="/benchmarks/:datasetId/:version/:split/:runId/images/:imageId"
                element={requireAdmin(<BenchmarkImageDetailPage />)}
              />
            </Routes>
          </div>
        </AuthenticatedQueryProvider>
      </AuthenticatedTemplate>
    </>
  );
}

function TabbedLayout({
  tabs,
  activeTab,
  onChange,
  children,
}: {
  tabs: TabDefinition[];
  activeTab: number;
  onChange: (val: string | number) => void;
  children: React.ReactNode;
}) {
  return (
    <Tabs activeTab={activeTab} onChange={onChange}>
      <Tabs.List>
        {tabs.map((t) => (
          <Tabs.Tab key={t.path}>{t.label}</Tabs.Tab>
        ))}
      </Tabs.List>
      <Tabs.Panels>
        {tabs.map((t, i) => (
          <Tabs.Panel key={t.path}>{activeTab === i ? children : null}</Tabs.Panel>
        ))}
      </Tabs.Panels>
    </Tabs>
  );
}

export default App;
