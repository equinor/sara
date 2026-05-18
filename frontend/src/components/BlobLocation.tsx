import type { BlobStorageLocation } from "../api/client";

export default function BlobLocation({
  loc,
}: {
  loc: BlobStorageLocation | null | undefined;
}) {
  if (!loc) return <span style={{ color: "#999" }}>–</span>;
  return (
    <span style={{ fontFamily: "monospace", fontSize: "0.85em" }}>
      {loc.storageAccount}/{loc.blobContainer}/{loc.blobName}
    </span>
  );
}
