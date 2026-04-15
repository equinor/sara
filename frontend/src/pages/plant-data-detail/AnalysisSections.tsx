import type { PlantData } from "../../api/client";
import WorkflowSection from "./WorkflowSection";

export default function AnalysisSections({ data }: { data: PlantData }) {
  return (
    <>
      <WorkflowSection
        title="CLOE Analysis"
        workflow={data.cloeAnalysis}
        extraFields={
          data.cloeAnalysis
            ? [
                { label: "Oil Level", value: data.cloeAnalysis.oilLevel?.toString() ?? "-" },
                { label: "Confidence", value: data.cloeAnalysis.confidence?.toString() ?? "-" },
              ]
            : undefined
        }
      />

      <WorkflowSection
        title="Fencilla Analysis"
        workflow={data.fencillaAnalysis}
        extraFields={
          data.fencillaAnalysis
            ? [
                {
                  label: "Is Break",
                  value: data.fencillaAnalysis.isBreak == null
                    ? "-"
                    : data.fencillaAnalysis.isBreak
                      ? "Yes"
                      : "No",
                },
                { label: "Confidence", value: data.fencillaAnalysis.confidence?.toString() ?? "-" },
              ]
            : undefined
        }
      />

      <WorkflowSection
        title="Thermal Reading Analysis"
        workflow={data.thermalReadingAnalysis}
        extraFields={
          data.thermalReadingAnalysis
            ? [
                { label: "Temperature", value: data.thermalReadingAnalysis.temperature?.toString() ?? "-" },
              ]
            : undefined
        }
      />
    </>
  );
}
