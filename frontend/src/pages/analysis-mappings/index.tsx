import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  Icon,
} from "@equinor/eds-core-react";
import { add, refresh } from "@equinor/eds-icons";
import {
  getAnalysisMappings,
  deleteAnalysisMapping,
  type AnalysisMapping,
} from "../../api/client";
import CreateMappingDialog from "./CreateMappingDialog";
import MappingsTable from "./MappingsTable";

Icon.add({ add, refresh });

export default function AnalysisMappingsPage() {
  const [data, setData] = useState<AnalysisMapping[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getAnalysisMappings();
      setData(result);
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Failed to fetch analysis mappings"
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleDelete = async (id: string) => {
    try {
      await deleteAnalysisMapping(id);
      await fetchData();
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Failed to delete analysis mapping"
      );
    }
  };

  return (
    <div style={{ paddingTop: "1rem" }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "1rem",
        }}
      >
        <Typography variant="h3">Analysis Mappings</Typography>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="ghost_icon"
            onClick={fetchData}
            aria-label="Refresh"
          >
            <Icon name="refresh" />
          </Button>
          <Button onClick={() => setShowCreate(true)}>
            <Icon name="add" />
            New Mapping
          </Button>
        </div>
      </div>

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
        <MappingsTable data={data} onDelete={handleDelete} />
      )}

      <CreateMappingDialog
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onCreated={fetchData}
        onError={setError}
      />
    </div>
  );
}
