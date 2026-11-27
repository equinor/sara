import { useRef, useState } from "react";
import type { ChangeEvent, KeyboardEvent } from "react";
import { Button, Icon, Progress, Typography } from "@equinor/eds-core-react";
import { image, restore, upload } from "@equinor/eds-icons";
import { tokens } from "@equinor/eds-tokens";
import styled from "styled-components";
import type { BenchmarkDataset } from "../../../../api/benchmarkClient";
import {
  useResetBenchmarkCoverImage,
  useUploadBenchmarkCoverImage,
} from "../../../../api/benchmarkClient";
import { ErrorText, Surface } from "../../../../components/Styles";
import { useBenchmarkCoverImageUrl } from "./useBenchmarkCoverImageUrl";

Icon.add({ image, restore, upload });

const MAX_COVER_IMAGE_BYTES = 5 * 1024 * 1024;
const ACCEPTED_IMAGE_TYPES = "image/png,image/jpeg,image/webp";

const CardContainer = styled(Surface)`
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition:
    border-color 150ms ease,
    box-shadow 150ms ease;

  &:hover {
    border-color: ${tokens.colors.interactive.primary__resting.hex};
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  }
`;

const ClickableArea = styled.div`
  display: flex;
  flex-direction: column;
  flex: 1;
  cursor: pointer;

  &:focus-visible {
    outline: 2px solid ${tokens.colors.interactive.focus.hex};
    outline-offset: -2px;
  }
`;

const MediaFrame = styled.div`
  position: relative;
  width: 100%;
  aspect-ratio: 16 / 9;
  background: ${tokens.colors.ui.background__light.hex};
  border-bottom: 1px solid ${tokens.colors.ui.background__medium.hex};
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
`;

const CoverImage = styled.img`
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
`;

const Placeholder = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.4rem;
  color: ${tokens.colors.text.static_icons__tertiary.hex};
`;

const CardBody = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  padding: 1rem;
  flex: 1;
`;

const Description = styled(Typography).attrs({ variant: "body_short" })`
  color: ${tokens.colors.text.static_icons__secondary.hex};
`;

const MetaText = styled(Typography).attrs({ variant: "caption" })`
  color: ${tokens.colors.text.static_icons__tertiary.hex};
  margin-top: auto;
  padding-top: 0.25rem;
`;

const CardFooter = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.5rem 0.75rem;
  border-top: 1px solid ${tokens.colors.ui.background__medium.hex};
  background: ${tokens.colors.ui.background__light.hex};
`;

const FooterButtons = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  flex-wrap: wrap;
`;

interface BenchmarkProjectCardProps {
  dataset: BenchmarkDataset;
  refreshToken: number;
  onSelect: () => void;
}

function formatVersionSummary(versions: string[]): string {
  if (versions.length === 0) return "No dataset versions yet";
  if (versions.length === 1) return `1 dataset version (${versions[0]})`;
  return `${versions.length} dataset versions (latest ${versions[0]})`;
}

export default function BenchmarkProjectCard({
  dataset,
  refreshToken,
  onSelect,
}: BenchmarkProjectCardProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [localRevision, setLocalRevision] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);

  const uploadMutation = useUploadBenchmarkCoverImage();
  const resetMutation = useResetBenchmarkCoverImage();
  const { imageUrl, loading, error: loadError } = useBenchmarkCoverImageUrl(
    dataset.id,
    refreshToken + localRevision
  );

  const busy = uploadMutation.isPending || resetMutation.isPending;

  const handleCardKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onSelect();
    }
  };

  const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;

    setActionError(null);
    if (file.size > MAX_COVER_IMAGE_BYTES) {
      setActionError("Image must be 5 MB or smaller.");
      return;
    }

    try {
      await uploadMutation.mutateAsync({ datasetId: dataset.id, file });
      setLocalRevision((prev) => prev + 1);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Failed to upload image.");
    }
  };

  const handleReset = async () => {
    setActionError(null);
    try {
      await resetMutation.mutateAsync(dataset.id);
      setLocalRevision((prev) => prev + 1);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Failed to reset image.");
    }
  };

  const displayError = actionError ?? loadError;

  return (
    <CardContainer>
      <ClickableArea
        role="link"
        tabIndex={0}
        onClick={onSelect}
        onKeyDown={handleCardKeyDown}
      >
        <MediaFrame>
          {loading ? (
            <Progress.Circular size={32} />
          ) : imageUrl ? (
            <CoverImage src={imageUrl} alt={`${dataset.label} preview`} />
          ) : (
            <Placeholder>
              <Icon name="image" size={32} />
              <Typography variant="caption">No preview image</Typography>
            </Placeholder>
          )}
        </MediaFrame>

        <CardBody>
          <Typography variant="h4">{dataset.label}</Typography>
          {dataset.description && <Description>{dataset.description}</Description>}
          <MetaText>{formatVersionSummary(dataset.versions)}</MetaText>
        </CardBody>
      </ClickableArea>

      <CardFooter>
        <input
          ref={fileInputRef}
          type="file"
          accept={ACCEPTED_IMAGE_TYPES}
          hidden
          onChange={handleFileChange}
        />
        <FooterButtons>
          <Button
            variant="ghost"
            disabled={busy}
            onClick={() => fileInputRef.current?.click()}
          >
            <Icon name="upload" size={16} />
            {uploadMutation.isPending ? "Uploading..." : "Change image"}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={handleReset}>
            <Icon name="restore" size={16} />
            {resetMutation.isPending ? "Resetting..." : "Reset"}
          </Button>
        </FooterButtons>
        {displayError && <ErrorText variant="caption">{displayError}</ErrorText>}
      </CardFooter>
    </CardContainer>
  );
}
