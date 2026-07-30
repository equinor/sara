import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import {
  Button,
  Icon,
  Table,
  Typography,
} from "@equinor/eds-core-react";
import { arrow_back, external_link } from "@equinor/eds-icons";
import { getInspectionRecord, type InspectionRecord, type Orientation, type Position } from "../../api/client";
import { getAppConfig } from "../../authConfig";
import StatusChip from "../../components/StatusChip";
import BlobLocation from "../../components/BlobLocation";

Icon.add({ arrow_back, external_link });

const fmt = (n: number) => n.toFixed(3);

function flotillaMissionUrl(
  installationCode: string | null | undefined,
  inspectionId: string | null | undefined,
  flotillaMissionId: string | null | undefined
): string | null {
  const base = getAppConfig().flotillaBaseUrl;
  if (!base || !installationCode) return null;
  // Prefer linking to the mission run page (resolves by Flotilla mission id),
  // highlighting the specific inspection via the query param when available.
  if (flotillaMissionId) {
    const url = `${base}/${encodeURIComponent(installationCode)}/mission/${encodeURIComponent(flotillaMissionId)}`;
    return inspectionId
      ? `${url}?inspectionId=${encodeURIComponent(inspectionId)}`
      : url;
  }
  // Fallback for older records without a mission id: the inspection-only route.
  if (!inspectionId) return null;
  return `${base}/${encodeURIComponent(installationCode)}/mission-simple?inspectionId=${encodeURIComponent(inspectionId)}`;
}

function formatPosition(p: Position | null | undefined): string {
  if (!p) return "–";
  return `x=${fmt(p.x)}, y=${fmt(p.y)}, z=${fmt(p.z)}`;
}

function formatOrientation(o: Orientation | null | undefined): string {
  if (!o) return "–";
  return `x=${fmt(o.x)}, y=${fmt(o.y)}, z=${fmt(o.z)}, w=${fmt(o.w)}`;
}

export default function InspectionRecordDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [record, setRecord] = useState<InspectionRecord | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    getInspectionRecord(id).then(setRecord).catch((e) =>
      setError(e instanceof Error ? e.message : "Failed to load")
    );
  }, [id]);

  if (error)
    return (
      <Typography variant="body_short" style={{ color: "#eb0000" }}>
        {error}
      </Typography>
    );
  if (!record) return <Typography variant="body_short">Loading…</Typography>;

  const flotillaUrl = flotillaMissionUrl(record.installationCode, record.inspectionId, record.flotillaMissionId);

  return (
    <div style={{ paddingTop: "1rem" }}>
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
        Inspection Record: {record.inspectionId}
      </Typography>

      <Table style={{ marginBottom: "1.5rem" }}>
        <Table.Body>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>{record.id}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Installation</Table.Cell>
            <Table.Cell>{record.installationCode}</Table.Cell>
          </Table.Row>
          {flotillaUrl && (
            <Table.Row>
              <Table.Cell>Flotilla</Table.Cell>
              <Table.Cell>
                <Button
                  variant="ghost"
                  href={flotillaUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  View mission in Flotilla
                  <Icon name="external_link" />
                </Button>
              </Table.Cell>
            </Table.Row>
          )}
          <Table.Row>
            <Table.Cell>Tag</Table.Cell>
            <Table.Cell>{record.tag ?? "–"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Type</Table.Cell>
            <Table.Cell>{record.inspectionType ?? "–"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Description</Table.Cell>
            <Table.Cell>{record.inspectionDescription ?? "–"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Robot</Table.Cell>
            <Table.Cell>{record.robotName ?? "–"}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Created</Table.Cell>
            <Table.Cell>{new Date(record.createdAt).toLocaleString()}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Timestamp</Table.Cell>
            <Table.Cell>
              {record.timestamp ? new Date(record.timestamp).toLocaleString() : "–"}
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Blob</Table.Cell>
            <Table.Cell>
              <BlobLocation loc={record.blobStorageLocation} />
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Target Position</Table.Cell>
            <Table.Cell>{formatPosition(record.targetPosition)}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Robot Pose</Table.Cell>
            <Table.Cell>
              {record.robotPose ? (
                <>
                  <div>pos: {formatPosition(record.robotPose.position)}</div>
                  <div>orient: {formatOrientation(record.robotPose.orientation)}</div>
                </>
              ) : (
                "–"
              )}
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Analysis Group</Table.Cell>
            <Table.Cell>
              {record.analysisGroupId ? (
                <Button
                  variant="ghost"
                  onClick={() => navigate(`/analysis-groups/${record.analysisGroupId}`)}
                >
                  {record.analysisGroupId}
                </Button>
              ) : (
                "–"
              )}
            </Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>

      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Analyses
      </Typography>
      <Table>
        <Table.Head>
          <Table.Row>
            <Table.Cell>Name</Table.Cell>
            <Table.Cell>Created</Table.Cell>
            <Table.Cell>#Runs</Table.Cell>
            <Table.Cell>Latest Run</Table.Cell>
            <Table.Cell></Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {(record.analyses ?? []).length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={5}>No analyses.</Table.Cell>
            </Table.Row>
          ) : (
            (record.analyses ?? []).map((a) => {
              const runs = a.runs ?? [];
              const latest = runs[runs.length - 1];
              return (
                <Table.Row key={a.id}>
                  <Table.Cell>{a.name}</Table.Cell>
                  <Table.Cell>{new Date(a.createdAt).toLocaleString()}</Table.Cell>
                  <Table.Cell>{runs.length}</Table.Cell>
                  <Table.Cell>
                    {latest ? <StatusChip status={latest.status} /> : "–"}
                  </Table.Cell>
                  <Table.Cell>
                    <Button variant="ghost" onClick={() => navigate(`/analyses/${a.id}`)}>
                      View
                    </Button>
                  </Table.Cell>
                </Table.Row>
              );
            })
          )}
        </Table.Body>
      </Table>
    </div>
  );
}
