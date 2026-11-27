import { useState } from "react";
import { useNavigate } from "react-router";
import { Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
import styled from "styled-components";
import { useBenchmarkDatasets } from "../../api/benchmarkClient";
import PageHeader from "../../components/PageHeader";
import { ErrorText, Surface } from "../../components/Styles";
import { benchmarkProjectPath } from "./benchmarks-components/benchmarkFormatting";
import BenchmarkProjectCard from "./benchmarks-components/projects/BenchmarkProjectCard";

const CardsGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.25rem;
`;

const SkeletonCard = styled(Surface)`
  height: 320px;
  background: ${tokens.colors.ui.background__light.hex};
`;

export default function BenchmarksPage() {
  const navigate = useNavigate();
  const datasetsQuery = useBenchmarkDatasets();
  const [refreshToken, setRefreshToken] = useState(0);

  const datasets = datasetsQuery.data ?? [];

  const handleRefresh = () => {
    datasetsQuery.refetch();
    setRefreshToken((prev) => prev + 1);
  };

  return (
    <PageHeader
      title="Benchmarks"
      loading={datasetsQuery.isFetching}
      onRefresh={handleRefresh}
    >
      {datasetsQuery.error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {datasetsQuery.error.message}
        </ErrorText>
      )}

      {datasetsQuery.isLoading ? (
        <CardsGrid>
          <SkeletonCard />
          <SkeletonCard />
        </CardsGrid>
      ) : datasets.length === 0 ? (
        <Typography variant="body_short">No benchmark projects configured.</Typography>
      ) : (
        <CardsGrid>
          {datasets.map((dataset) => (
            <BenchmarkProjectCard
              key={dataset.id}
              dataset={dataset}
              refreshToken={refreshToken}
              onSelect={() => navigate(benchmarkProjectPath(dataset.id))}
            />
          ))}
        </CardsGrid>
      )}
    </PageHeader>
  );
}
