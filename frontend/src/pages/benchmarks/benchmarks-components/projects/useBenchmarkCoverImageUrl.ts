import { useEffect, useState } from "react";
import { getBenchmarkCoverImageUrl } from "../../../../api/benchmarkClient";

export function useBenchmarkCoverImageUrl(datasetId: string, revision: number) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let objectUrl: string | null = null;
    setLoading(true);
    setError(null);

    getBenchmarkCoverImageUrl(datasetId, controller.signal)
      .then((url) => {
        if (controller.signal.aborted) {
          if (url) URL.revokeObjectURL(url);
          return;
        }
        objectUrl = url;
        setImageUrl(url);
        setLoading(false);
      })
      .catch((err) => {
        if (controller.signal.aborted) return;
        setImageUrl(null);
        setError(err instanceof Error ? err.message : "Failed to load cover image");
        setLoading(false);
      });

    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [datasetId, revision]);

  return { imageUrl, loading, error };
}
