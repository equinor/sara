import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Button,
  Icon,
  Table,
  Typography,
} from "@equinor/eds-core-react";
import { add, refresh } from "@equinor/eds-icons";
import { useNavigate } from "react-router";
import styled from "styled-components";
import {
  deleteThermalReferenceMetadata,
  getThermalReferenceMetadata,
  type ThermalReferenceMetadata,
} from "../../api/client";

Icon.add({ add, refresh });

const StyledPageHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
`;

const StyledActions = styled.div`
  display: flex;
  gap: 0.5rem;
`;

export default function ThermalReferenceImagesPage() {
  const navigate = useNavigate();
  const [data, setData] = useState<ThermalReferenceMetadata[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getThermalReferenceMetadata();
      setData(result);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to fetch thermal reference metadata"
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const sortedData = useMemo(
    () =>
      [...data].sort(
        (left, right) =>
          new Date(right.dateCreated).getTime() - new Date(left.dateCreated).getTime()
      ),
    [data]
  );

  const handleDelete = async (id: string) => {
    try {
      await deleteThermalReferenceMetadata(id);
      await fetchData();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Failed to delete thermal reference metadata"
      );
    }
  };

  return (
    <div style={{ paddingTop: "1rem" }}>
      <StyledPageHeader>
        <Typography variant="h3">Thermal Reference Metadata</Typography>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="ghost_icon"
            onClick={fetchData}
            aria-label="Refresh"
            disabled={loading}
          >
            <Icon name="refresh" />
          </Button>
          <Button onClick={() => navigate("/create-thermal-reference-metadata")}>
            <Icon name="add" />
            New Reference Metadata
          </Button>
        </div>
      </StyledPageHeader>

      {error && (
        <Typography
          variant="body_short"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      {loading ? (
        <Typography variant="body_short">Loading...</Typography>
      ) : (
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Installation</Table.Cell>
              <Table.Cell>Tag</Table.Cell>
              <Table.Cell>Inspection Description</Table.Cell>
              <Table.Cell>Actions</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {sortedData.map((metadata) => (
              <Table.Row
                key={metadata.id}
                onClick={() => navigate(`/thermal-reference-images/${metadata.id}`)}
                style={{ cursor: "pointer" }}
              >
                <Table.Cell>{metadata.installationCode}</Table.Cell>
                <Table.Cell>{metadata.tagId}</Table.Cell>
                <Table.Cell>{metadata.inspectionDescription}</Table.Cell>
                <Table.Cell>
                  <StyledActions>
                    <Button
                      variant="ghost"
                      color="danger"
                      onClick={(event) => {
                        event.stopPropagation();
                        handleDelete(metadata.id);
                      }}
                    >
                      Delete
                    </Button>
                  </StyledActions>
                </Table.Cell>
              </Table.Row>
            ))}
            {sortedData.length === 0 && (
              <Table.Row>
                <Table.Cell colSpan={4}>
                  <Typography variant="body_short">
                    No thermal reference metadata found.
                  </Typography>
                </Table.Cell>
              </Table.Row>
            )}
          </Table.Body>
        </Table>
      )}
    </div>
  );
}
