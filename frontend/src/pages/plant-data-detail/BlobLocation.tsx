import { Typography } from "@equinor/eds-core-react";
import type { BlobStorageLocation } from "../../api/client";

export default function BlobLocation({ location }: { location: BlobStorageLocation | null | undefined }) {
  if (!location) return <Typography variant="body_short">-</Typography>;
  const isEmpty = !location.storageAccount && !location.blobContainer && !location.blobName;
  if (isEmpty) return <Typography variant="body_short" style={{ color: "#6f6f6f" }}>Not set</Typography>;
  return (
    <Typography variant="body_short" style={{ fontFamily: "monospace", fontSize: "0.85rem" }}>
      {location.storageAccount}/{location.blobContainer}/{location.blobName}
    </Typography>
  );
}
