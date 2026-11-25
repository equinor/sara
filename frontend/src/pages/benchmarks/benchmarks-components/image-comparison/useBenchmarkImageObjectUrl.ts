import { useEffect, useState } from "react";
import { getBenchmarkImageUrl } from "../../../../api/benchmarkClient";

export function useBenchmarkImageObjectUrl(
  datasetId: string,
  version: string,
  split: string,
  runId: string,
  imageId: number | null
) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);

  useEffect(() => {
    setImageError(null);
    setImageUrl(null);
    if (imageId === null) return;
    let cancelled = false;
    let objectUrl: string | null = null;

    getBenchmarkImageUrl(datasetId, version, split, runId, imageId)
      .then((url) => {
        if (cancelled) {
          URL.revokeObjectURL(url);
          return;
        }
        objectUrl = url;
        setImageUrl(url);
      })
      .catch((err) => {
        if (!cancelled) {
          setImageError(err instanceof Error ? err.message : "Failed to load image");
        }
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [datasetId, version, split, runId, imageId]);

  return { imageUrl, imageError };
}
