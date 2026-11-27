import type { ChangeEvent } from "react";
import { Button, TextField, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { BenchmarkDataset } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import { ErrorText, FilterBar, FilterSelect } from "../../../../components/Styles";
import { formatMethodLabel } from "../benchmarkFormatting";

const SubmitButton = styled(Button)`
  flex: 0 0 auto !important;
  white-space: nowrap;
`;

interface BenchmarkTriggerPanelProps {
  datasets: BenchmarkDataset[];
  datasetId: string;
  version: string;
  split: string;
  device: string;
  method: string;
  confidenceThreshold: string;
  boxPadding: string;
  runId: string;
  submitting: boolean;
  error: string | null;
  lastTriggeredWorkflow: string | null;
  onDatasetChange: (value: string) => void;
  onVersionChange: (value: string) => void;
  onSplitChange: (value: string) => void;
  onDeviceChange: (value: string) => void;
  onMethodChange: (value: string) => void;
  onConfidenceThresholdChange: (value: string) => void;
  onBoxPaddingChange: (value: string) => void;
  onRunIdChange: (value: string) => void;
  onSubmit: () => void;
}

export default function BenchmarkTriggerPanel({
  datasets,
  datasetId,
  version,
  split,
  device,
  method,
  confidenceThreshold,
  boxPadding,
  runId,
  submitting,
  error,
  lastTriggeredWorkflow,
  onDatasetChange,
  onVersionChange,
  onSplitChange,
  onDeviceChange,
  onMethodChange,
  onConfidenceThresholdChange,
  onBoxPaddingChange,
  onRunIdChange,
  onSubmit,
}: BenchmarkTriggerPanelProps) {
  const selectedDataset = datasets.find((d) => d.id === datasetId);
  const versions = selectedDataset?.versions ?? [];
  const devices = selectedDataset?.devices ?? [];
  const methods = selectedDataset?.methods ?? [];
  const showBoundingBoxParams = method === "bounding_box";
  const canSubmit = Boolean(datasetId && version && split && device && !submitting);

  return (
    <DashboardPanel title="Run benchmark" style={{ marginBottom: "1rem" }}>
      <FilterBar style={{ marginBottom: error || lastTriggeredWorkflow ? "0.5rem" : 0 }}>
        <FilterSelect
          id="benchmark-dataset"
          label="Dataset"
          value={datasetId}
          onChange={(e) => onDatasetChange(e.target.value)}
        >
          {datasets.map((d) => (
            <option key={d.id} value={d.id}>
              {d.label} ({d.container})
            </option>
          ))}
        </FilterSelect>

        {versions.length > 0 ? (
          <FilterSelect
            id="benchmark-version"
            label="Version"
            value={version}
            onChange={(e) => onVersionChange(e.target.value)}
          >
            {versions.map((v) => (
              <option key={v} value={v}>
                {v}
              </option>
            ))}
          </FilterSelect>
        ) : (
          <TextField
            id="benchmark-version"
            label="Version"
            value={version}
            placeholder="v001"
            onChange={(e: ChangeEvent<HTMLInputElement>) =>
              onVersionChange(e.target.value)
            }
          />
        )}

        <TextField
          id="benchmark-split"
          label="Split"
          value={split}
          placeholder="val"
          onChange={(e: ChangeEvent<HTMLInputElement>) =>
            onSplitChange(e.target.value)
          }
        />

        <FilterSelect
          id="benchmark-device"
          label="Device"
          value={device}
          onChange={(e) => onDeviceChange(e.target.value)}
        >
          {devices.map((dev) => (
            <option key={dev} value={dev}>
              {dev.toUpperCase()}
            </option>
          ))}
        </FilterSelect>

        {methods.length > 0 && (
          <FilterSelect
            id="benchmark-method"
            label="Method"
            value={method}
            onChange={(e) => onMethodChange(e.target.value)}
          >
            {methods.map((m) => (
              <option key={m} value={m}>
                {formatMethodLabel(m)}
              </option>
            ))}
          </FilterSelect>
        )}

        {showBoundingBoxParams && (
          <>
            <TextField
              id="benchmark-confidence"
              label="Confidence"
              type="number"
              step="0.05"
              min="0.01"
              max="1"
              value={confidenceThreshold}
              placeholder="0.3"
              onChange={(e: ChangeEvent<HTMLInputElement>) =>
                onConfidenceThresholdChange(e.target.value)
              }
            />
            <TextField
              id="benchmark-box-padding"
              label="Box padding"
              type="number"
              step="0.05"
              min="0"
              max="1"
              value={boxPadding}
              placeholder="0.1"
              onChange={(e: ChangeEvent<HTMLInputElement>) =>
                onBoxPaddingChange(e.target.value)
              }
            />
          </>
        )}

        <TextField
          id="benchmark-run-id"
          label="Run ID (optional)"
          value={runId}
          placeholder="Auto (Argo workflow name)"
          onChange={(e: ChangeEvent<HTMLInputElement>) =>
            onRunIdChange(e.target.value)
          }
        />

        <SubmitButton onClick={onSubmit} disabled={!canSubmit}>
          {submitting ? "Starting..." : "Run benchmark"}
        </SubmitButton>
      </FilterBar>

      {error && <ErrorText variant="body_short">{error}</ErrorText>}
      {lastTriggeredWorkflow && !error && (
        <Typography variant="body_short">
          Started workflow <strong>{lastTriggeredWorkflow}</strong>.
        </Typography>
      )}
    </DashboardPanel>
  );
}
