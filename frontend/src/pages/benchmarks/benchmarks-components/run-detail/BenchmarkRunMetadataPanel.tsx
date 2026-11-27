import { Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { BenchmarkRunMetadata } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import { formatConfigurationSummary, shortArtifact } from "../benchmarkFormatting";

const MetadataGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 0.75rem;
`;

const MetadataValue = styled(Typography).attrs({ variant: "body_short" })`
  word-break: break-word;
`;

interface BenchmarkRunMetadataPanelProps {
  run: BenchmarkRunMetadata;
}

export default function BenchmarkRunMetadataPanel({
  run,
}: BenchmarkRunMetadataPanelProps) {
  const configSummary = formatConfigurationSummary(run.configuration);

  return (
    <DashboardPanel title="Run metadata" style={{ marginBottom: "1rem" }}>
      <MetadataGrid>
        <div>
          <Typography variant="caption">Dataset</Typography>
          <MetadataValue>
            {run.dataset.id} / {run.dataset.version} / {run.dataset.split}
          </MetadataValue>
        </div>
        {configSummary && (
          <div>
            <Typography variant="caption">Configuration</Typography>
            <MetadataValue>
              {configSummary.details
                ? `${configSummary.primary} (${configSummary.details})`
                : configSummary.primary}
            </MetadataValue>
          </div>
        )}
        <div>
          <Typography variant="caption">Artifact</Typography>
          <MetadataValue title={run.artifact}>
            {shortArtifact(run.artifact)}
          </MetadataValue>
        </div>
        <div>
          <Typography variant="caption">Hardware</Typography>
          <MetadataValue>
            {run.execution.hardware} ({run.execution.timing_scope})
          </MetadataValue>
        </div>
        <div>
          <Typography variant="caption">Protocol</Typography>
          <MetadataValue>{run.protocol}</MetadataValue>
        </div>
        <div>
          <Typography variant="caption">Manifest SHA-256</Typography>
          <MetadataValue style={{ wordBreak: "break-all" }}>
            {run.dataset.manifest_sha256}
          </MetadataValue>
        </div>
      </MetadataGrid>
    </DashboardPanel>
  );
}
