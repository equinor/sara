import { Table } from "@equinor/eds-core-react";
import { useNavigate } from "react-router";
import type { StuckWorkflow } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import IdCell from "../../../components/IdCell";
import { statusColors } from "../../../components/Styles";
import OverviewDenseTable from "./OverviewDenseTable";

export default function StuckWorkflowsPanel({ workflows }: { workflows: StuckWorkflow[] }) {
  const navigate = useNavigate();
  if (workflows.length === 0) return null;
  return (
    <DashboardPanel
      title={`Possibly stuck workflows (${workflows.length})`}
      accent={statusColors.warning.accent}
    >
      <OverviewDenseTable>
        <Table.Head>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Running for</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {workflows.map((workflow) => (
            <Table.Row
              key={workflow.id}
              onClick={() => navigate(`/workflows/${workflow.id}`)}
              style={{ cursor: "pointer" }}
            >
              <Table.Cell><IdCell id={workflow.id} /></Table.Cell>
              <Table.Cell>{workflow.workflowType}</Table.Cell>
              <Table.Cell>{Math.round(workflow.minutesRunning)} min</Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </OverviewDenseTable>
    </DashboardPanel>
  );
}
