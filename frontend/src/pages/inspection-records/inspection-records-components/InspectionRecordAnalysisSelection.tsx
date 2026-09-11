import { Checkbox, Typography } from "@equinor/eds-core-react";
import type { AnalysisConfigEntry } from "../../../api/client";
import { ErrorText } from "../../../components/Styles";

interface InspectionRecordAnalysisSelectionProps {
  configured: AnalysisConfigEntry[];
  isPending: boolean;
  error: Error | null;
  selectedAnalyses: Set<string>;
  toggleAnalysis: (name: string) => void;
}

export default function InspectionRecordAnalysisSelection({
  configured, isPending, error, selectedAnalyses, toggleAnalysis,
}: InspectionRecordAnalysisSelectionProps) {
  return (
    <>
      <Typography variant="h5" style={{ marginBottom: "0.5rem" }}>
        Analyses to Run
      </Typography>
      <Typography variant="body_short" style={{ marginBottom: "0.5rem", color: "#666" }}>
        Leave all unchecked to use file-extension defaults.
      </Typography>
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "0.5rem 1.25rem",
          marginBottom: "1.5rem",
        }}
      >
        {isPending ? (
          <Typography variant="body_short">Loading configured analyses...</Typography>
        ) : error ? (
          <ErrorText variant="body_short" role="alert">
            Failed to load configured analyses: {error.message}
          </ErrorText>
        ) : configured.length === 0 ? (
          <Typography variant="body_short">No configured analyses found.</Typography>
        ) : (
          configured.map((entry) => (
            <Checkbox
              key={entry.name}
              label={`${entry.name} (${entry.workflows.join(" → ") || "no workflows"})`}
              checked={selectedAnalyses.has(entry.name)}
              onChange={() => toggleAnalysis(entry.name)}
            />
          ))
        )}
      </div>
    </>
  );
}
