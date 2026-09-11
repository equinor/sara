import { Button, Table } from "@equinor/eds-core-react";
import { useNavigate } from "react-router";
import type { Workflow } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import IdCell from "../../../components/IdCell";
import { statusColors } from "../../../components/Styles";
import OverviewDenseTable from "./OverviewDenseTable";

export default function RecentFailuresPanel({ failures, retryPending, onRetry }: {
  failures: Workflow[];
  retryPending: boolean;
  onRetry: (id: string) => Promise<void>;
}) {
  const navigate = useNavigate();
  if (failures.length === 0) return null;
  return (
    <DashboardPanel title="Recent failures" accent={statusColors.error.accent}>
      <OverviewDenseTable>
        <Table.Head>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Error</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {failures.map((workflow) => (
            <Table.Row
              key={workflow.id}
              onClick={() => navigate(`/workflows/${workflow.id}`)}
              style={{ cursor: "pointer" }}
            >
              <Table.Cell><IdCell id={workflow.id} /></Table.Cell>
              <Table.Cell>{workflow.workflowType}</Table.Cell>
              <Table.Cell
                style={{ maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
              >
                {workflow.errorMessage ?? "\u2013"}
              </Table.Cell>
              <Table.Cell>
                <Button
                  variant="ghost"
                  disabled={retryPending}
                  onClick={(event) => {
                    event.stopPropagation();
                    onRetry(workflow.id);
                  }}
                >
                  Retry
                </Button>
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </OverviewDenseTable>
    </DashboardPanel>
  );
}
