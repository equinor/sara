import { useNavigate } from "react-router";
import { ErrorText } from "../../components/Styles";
import {
  deleteInspectionRecord,
  getInspectionRecords,
  type InspectionRecord,
  type InspectionRecordParams,
} from "../../api/client";
import { useResourceMutation } from "../../api/queries";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import InspectionRecordFilters from "./inspection-records-components/InspectionRecordFilters";
import InspectionRecordsTable from "./inspection-records-components/InspectionRecordsTable";

const FILTER_KEYS: (keyof InspectionRecordParams & string)[] = [
  "inspectionId",
  "tag",
  "installationCode",
];

export default function InspectionRecordsPage() {
  const navigate = useNavigate();
  const deleteMutation = useResourceMutation(deleteInspectionRecord, "delete");
  const {
    response,
    loading,
    initialLoading,
    error,
    pageNumber,
    pageSize,
    filters,
    setPage,
    setPageSize,
    setFilters,
    refetch,
  } = usePagedList<InspectionRecord, InspectionRecordParams>(
    "inspection-records",
    "inspectionRecords.pageSize",
    FILTER_KEYS,
    getInspectionRecords
  );

  const items = response?.items ?? [];

  const handleDelete = async (id: string) => {
    if (deleteMutation.isPending) return;
    if (!window.confirm("Delete this inspection record and its analyses?")) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader
      title="Inspection Records"
      loading={loading}
      onRefresh={refetch}
      primaryAction={{
        label: "New Inspection Record",
        onClick: () => navigate("/inspection-records/new"),
      }}
    >
      <InspectionRecordFilters filters={filters} setFilters={setFilters} />

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
      )}

      <InspectionRecordsTable
        items={items}
        initialLoading={initialLoading}
        pageSize={pageSize}
        deleting={deleteMutation.isPending}
        navigate={navigate}
        onDelete={handleDelete}
      />

      <PaginationFooter
        hasResponse={response !== null}
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={response?.totalCount ?? null}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        disabled={loading}
        loading={loading}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        resetKey={`${pageSize}-${pageNumber}`}
      />
    </PageHeader>
  );
}
