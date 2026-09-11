import { useNavigate, useParams } from "react-router";
import { Button, Icon, Table, Typography } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { getAnalysisGroup } from "../../api/client";
import { useResourceDetail } from "../../api/queries";
import StatusChip from "../../components/StatusChip";

Icon.add({ arrow_back });

export default function AnalysisGroupDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: group, error, isPending } = useResourceDetail("analysis-groups", id, getAnalysisGroup);

  if (!id || (error && !group))
    return (
      <Typography variant="body_short" style={{ color: "#eb0000" }}>
        {!id ? "Missing analysis group ID" : error?.message ?? "Failed to load"}
      </Typography>
    );
  if (isPending || !group) return <Typography variant="body_short">Loading…</Typography>;

  return (
    <div style={{ paddingTop: "1rem" }}>
      {error && (
        <Typography variant="body_short" role="alert" style={{ color: "#eb0000" }}>
          {error.message}
        </Typography>
      )}
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
        Analysis Group: {group.groupId}
      </Typography>

      <Table style={{ marginBottom: "1.5rem" }}>
        <Table.Body>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>{group.id}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>
              <StatusChip status={group.status} />
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Expected Size</Table.Cell>
            <Table.Cell>{group.expectedSize}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Timeout</Table.Cell>
            <Table.Cell>
              {group.timeoutAt ? new Date(group.timeoutAt).toLocaleString() : "–"}
            </Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Inspection Records
      </Typography>
      <Table style={{ marginBottom: "1.5rem" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Inspection ID</Table.Cell>
            <Table.Cell>Tag</Table.Cell>
            <Table.Cell>Installation</Table.Cell>
            <Table.Cell></Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {(group.inspectionRecords ?? []).length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={4}>None.</Table.Cell>
            </Table.Row>
          ) : (
            (group.inspectionRecords ?? []).map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>{r.inspectionId}</Table.Cell>
                <Table.Cell>{r.tag ?? "–"}</Table.Cell>
                <Table.Cell>{r.installationCode}</Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    onClick={() => navigate(`/inspection-records/${r.id}`)}
                  >
                    View
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Analyses
      </Typography>
      <Table>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>Created</Table.Cell>
            <Table.Cell>#Runs</Table.Cell>
            <Table.Cell></Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {(group.analyses ?? []).length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={4}>None.</Table.Cell>
            </Table.Row>
          ) : (
            (group.analyses ?? []).map((a) => (
              <Table.Row key={a.id}>
                <Table.Cell>{a.analysisType}</Table.Cell>
                <Table.Cell>{new Date(a.createdAt).toLocaleString()}</Table.Cell>
                <Table.Cell>{(a.runs ?? []).length}</Table.Cell>
                <Table.Cell>
                  <Button variant="ghost" onClick={() => navigate(`/analyses/${a.id}`)}>
                    View
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>
    </div>
  );
}
