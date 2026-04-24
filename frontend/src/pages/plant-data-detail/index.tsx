import { useEffect, useState, useCallback } from "react";
import {
  Button,
  Typography,
  Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate, useParams } from "react-router";
import {
  getPlantDataById,
  triggerAnonymizer,
  type PlantData,
} from "../../api/client";
import GeneralInfoTable from "./GeneralInfoTable";
import AnonymizationSection from "./AnonymizationSection";
import styled from "styled-components";

const StyledBackNavRow = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
`;

const StyledBackNavRowLg = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
`;
import AnalysisSections from "./AnalysisSections";

Icon.add({ arrow_back });

export default function PlantDataDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<PlantData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [triggering, setTriggering] = useState(false);

  const fetchData = useCallback(async () => {
    if (!id) return;
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

  const navigateBack = () => navigate("/plant-data");

  const handleTriggerWorkflow = async (plantData: PlantData) => {
    setTriggering(true);
    try {
      await triggerAnonymizer(plantData.id);
      await fetchData();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to trigger workflow chain");
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
        <StyledBackNavRow>
          <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
            <Icon name="arrow_back" />
          </Button>
          <Typography variant="h3">Plant Data</Typography>
        </StyledBackNavRow>
        <Typography variant="body_short" style={{ color: "#eb0000" }}>
          {error ?? "Not found"}
        </Typography>
      </div>
    );
  }

  return (
    <div style={{ paddingTop: "1rem", maxWidth: 900 }}>
      <StyledBackNavRowLg>
        <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
          <Icon name="arrow_back" />
        </Button>
        <Typography variant="h3">{data.inspectionId}</Typography>
      </StyledBackNavRowLg>

      <GeneralInfoTable data={data} />
      <AnonymizationSection data={data} triggering={triggering} onTrigger={handleTriggerWorkflow} />
      <AnalysisSections data={data} />
    </div>
  );
}
