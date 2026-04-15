import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  Icon,
} from "@equinor/eds-core-react";
import { add, refresh } from "@equinor/eds-icons";
import { useNavigate } from "react-router";
import {
  getPlantData,
  triggerAnonymizer,
  type PlantData,
} from "../../api/client";
import PlantDataTable from "./PlantDataTable";

Icon.add({ add, refresh });

export default function PlantDataPage() {
  const navigate = useNavigate();
  const [data, setData] = useState<PlantData[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getPlantData();
      setData(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to fetch plant data");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleTriggerAnonymizer = async (id: string) => {
    setTriggeringId(id);
    try {
      await triggerAnonymizer(id);
      await fetchData();
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Failed to trigger anonymizer"
      );
    } finally {
      setTriggeringId(null);
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
        <Typography variant="h3">Plant Data</Typography>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="ghost_icon"
            onClick={fetchData}
            aria-label="Refresh"
          >
            <Icon name="refresh" />
          </Button>
          <Button onClick={() => navigate("/create-plant-data")}>
            <Icon name="add" />
            New Plant Data
          </Button>
        </div>
      </div>

      {error && (
        <Typography
          variant="body_short"
          color="danger"
          style={{ marginBottom: "1rem", color: "#eb0000" }}
        >
          {error}
        </Typography>
      )}

      {loading ? (
        <Typography variant="body_short">Loading...</Typography>
      ) : (
        <PlantDataTable
          data={data}
          triggeringId={triggeringId}
          onTriggerAnonymizer={handleTriggerAnonymizer}
        />
      )}
    </div>
  );
}
