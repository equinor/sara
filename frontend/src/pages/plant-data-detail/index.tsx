import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import {
  getPlantDataById,
  triggerAnonymizer,
  type PlantData,
} from "../../api/client";
import GeneralInfoTable from "./GeneralInfoTable";
import AnonymizationSection from "./AnonymizationSection";
import AnalysisSections from "./AnalysisSections";

Icon.add({ arrow_back });

export default function PlantDataDetailPage({ id }: { id: string }) {
  const [data, setData] = useState<PlantData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [triggering, setTriggering] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getPlantDataById(id);
      setData(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to fetch plant data");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const navigateBack = () => {
    window.history.pushState(null, "", "/plant-data");
    window.dispatchEvent(new PopStateEvent("popstate"));
  };

  const handleTriggerAnonymizer = async () => {
    if (!data) return;
    setTriggering(true);
    try {
      await triggerAnonymizer(data.id);
      await fetchData();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to trigger anonymizer");
    } finally {
      setTriggering(false);
    }
  };

  if (loading) {
    return (
      <div style={{ paddingTop: "1rem" }}>
        <Typography variant="body_short">Loading...</Typography>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div style={{ paddingTop: "1rem" }}>
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "1rem" }}>
          <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
            <Icon name="arrow_back" />
          </Button>
          <Typography variant="h3">Plant Data</Typography>
        </div>
        <Typography variant="body_short" style={{ color: "#eb0000" }}>
          {error ?? "Not found"}
        </Typography>
      </div>
    );
  }

  return (
    <div style={{ paddingTop: "1rem", maxWidth: 900 }}>
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "1.5rem" }}>
        <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
          <Icon name="arrow_back" />
        </Button>
        <Typography variant="h3">{data.inspectionId}</Typography>
      </div>

      <GeneralInfoTable data={data} />
      <AnonymizationSection data={data} triggering={triggering} onTrigger={handleTriggerAnonymizer} />
      <AnalysisSections data={data} />
    </div>
  );
}
