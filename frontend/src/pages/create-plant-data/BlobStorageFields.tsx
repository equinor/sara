import { Typography, TextField } from "@equinor/eds-core-react";
import type { BlobStorageLocation } from "../../api/client";

interface BlobStorageFieldsProps {
  location: BlobStorageLocation;
  onChange: (location: BlobStorageLocation) => void;
}

export default function BlobStorageFields({ location, onChange }: BlobStorageFieldsProps) {
  return (
    <>
      <Typography variant="h6">Raw Data Blob Storage</Typography>
      <TextField
        id="storageAccount"
        label="Storage Account"
        value={location.storageAccount}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
          onChange({ ...location, storageAccount: e.target.value })
        }
      />
      <TextField
        id="blobContainer"
        label="Blob Container"
        value={location.blobContainer}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
          onChange({ ...location, blobContainer: e.target.value })
        }
      />
      <TextField
        id="blobName"
        label="Blob Name"
        value={location.blobName}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
          onChange({ ...location, blobName: e.target.value })
        }
      />
    </>
  );
}
