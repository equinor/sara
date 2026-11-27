import type { ChangeEvent } from "react";
import { Button, TextField, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { BenchmarkDataset } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import { ErrorText, FilterBar, FilterSelect } from "../../../../components/Styles";
import { formatMethodLabel, isParameterApplicable } from "../benchmarkFormatting";

const SubmitButton = styled(Button)`
  flex: 0 0 auto !important;
  white-space: nowrap;
`;

interface BenchmarkTriggerPanelProps {
  dataset?: BenchmarkDataset;
  version: string;
  split: string;
  device: string;
  method: string;
  parameterValues: Record<string, string>;
  runId: string;
  submitting: boolean;
  error: string | null;
  lastTriggeredWorkflow: string | null;
  onVersionChange: (value: string) => void;
  onSplitChange: (value: string) => void;
  onDeviceChange: (value: string) => void;
  onMethodChange: (value: string) => void;
  onParameterChange: (name: string, value: string) => void;
  onRunIdChange: (value: string) => void;
  onSubmit: () => void;
}

export default function BenchmarkTriggerPanel({
  dataset,
  version,
  split,
  device,
  method,
  parameterValues,
  runId,
  submitting,
  error,
  lastTriggeredWorkflow,
  onVersionChange,
  onSplitChange,
  onDeviceChange,
  onMethodChange,
  onParameterChange,
  onRunIdChange,
  onSubmit,
}: BenchmarkTriggerPanelProps) {
  const versions = dataset?.versions ?? [];
  const devices = dataset?.devices ?? [];
  const methods = dataset?.methods ?? [];
  const visibleParameters = (dataset?.parameters ?? []).filter((param) =>
    isParameterApplicable(param, method)
  );
  const canSubmit = Boolean(dataset && version && split && device && !submitting);

  return (
    <DashboardPanel title="Run benchmark" style={{ marginBottom: "1rem" }}>
      <FilterBar style={{ marginBottom: error || lastTriggeredWorkflow ? "0.5rem" : 0 }}>
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

        {visibleParameters.map((param) => {
          const minAttr =
            param.min == null
              ? undefined
              : String(param.minExclusive ? (param.step ?? 0.01) : param.min);
          return (
            <TextField
              key={param.name}
              id={`benchmark-param-${param.name}`}
              label={param.label}
              type="number"
              step={param.step != null ? String(param.step) : "any"}
              min={minAttr}
              max={param.max != null ? String(param.max) : undefined}
              value={parameterValues[param.name] ?? ""}
              placeholder={param.placeholder ?? undefined}
              onChange={(e: ChangeEvent<HTMLInputElement>) =>
                onParameterChange(param.name, e.target.value)
              }
            />
          );
        })}

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
