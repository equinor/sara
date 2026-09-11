import { useNavigate, useParams } from "react-router";
import {
  Button,
  Icon,
  Typography,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { getInspectionRecord } from "../../api/client";
import { useResourceDetail } from "../../api/queries";
import { ErrorText } from "../../components/Styles";
import InspectionRecordMetadata from "./inspection-records-components/InspectionRecordMetadata";
import InspectionRecordAnalyses from "./inspection-records-components/InspectionRecordAnalyses";

Icon.add({ arrow_back });

export default function InspectionRecordDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: record, error, isPending } = useResourceDetail("inspection-records", id, getInspectionRecord);

  if (!id || (error && !record))
    return (
      <ErrorText variant="body_short">
        {!id ? "Missing inspection record ID" : error?.message ?? "Failed to load"}
      </ErrorText>
    );
  if (isPending || !record) return <Typography variant="body_short">Loading…</Typography>;

  return (
    <div style={{ paddingTop: "1rem" }}>
      {error && (
        <ErrorText variant="body_short" role="alert">
          {error.message}
        </ErrorText>
      )}
      <Button variant="ghost" onClick={() => navigate(-1)}>
        <Icon name="arrow_back" /> Back
      </Button>
      <Typography variant="h3" style={{ margin: "0.5rem 0" }}>
        Inspection Record: {record.inspectionId}
      </Typography>

      <InspectionRecordMetadata record={record} navigate={navigate} />
      <InspectionRecordAnalyses analyses={record.analyses} navigate={navigate} />
    </div>
  );
}
