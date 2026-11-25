import { useMemo, useState } from "react";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import type { BenchmarkImageResult } from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import PaginationFooter from "../../../../components/PaginationFooter";
import SegmentedToggle from "../../../../components/SegmentedToggle";
import StatusChip from "../../../../components/StatusChip";
import { FilterBar, FilterSearch, TableScroller } from "../../../../components/Styles";
import { formatNumber, isImagePassed } from "../benchmarkFormatting";

type ImageOutcomeFilter = "all" | "failed" | "missed" | "fp" | "passed";

const PAGE_SIZE_OPTIONS = [25, 50, 100];

interface BenchmarkPerImageTableProps {
  items: BenchmarkImageResult[];
  onSelectImage: (imageId: number) => void;
}

export default function BenchmarkPerImageTable({
  items,
  onSelectImage,
}: BenchmarkPerImageTableProps) {
  const [filter, setFilter] = useState<ImageOutcomeFilter>("all");
  const [search, setSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const counts = useMemo(() => {
    let failed = 0;
    let missed = 0;
    let fp = 0;
    let passed = 0;
    for (const row of items) {
      if (isImagePassed(row)) passed += 1;
      else failed += 1;
      if (row.missed > 0) missed += 1;
      if (row.false_positives > 0) fp += 1;
    }
    return { all: items.length, failed, missed, fp, passed };
  }, [items]);

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    return items.filter((row) => {
      if (filter === "failed" && isImagePassed(row)) return false;
      if (filter === "passed" && !isImagePassed(row)) return false;
      if (filter === "missed" && row.missed === 0) return false;
      if (filter === "fp" && row.false_positives === 0) return false;
      if (
        query &&
        !row.file_name.toLowerCase().includes(query) &&
        !String(row.image_id).includes(query)
      ) {
        return false;
      }
      return true;
    });
  }, [items, filter, search]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const safePage = Math.min(pageNumber, totalPages);
  const pageItems = filtered.slice((safePage - 1) * pageSize, safePage * pageSize);

  const handleFilterChange = (next: ImageOutcomeFilter) => {
    setFilter(next);
    setPageNumber(1);
  };

  return (
    <DashboardPanel title={`Per-image results (${filtered.length} / ${items.length})`}>
      <FilterBar style={{ alignItems: "center" }}>
        <div style={{ flex: "0 0 auto" }}>
          <SegmentedToggle<ImageOutcomeFilter>
            value={filter}
            onChange={handleFilterChange}
            options={[
              { value: "all", label: `All (${counts.all})` },
              { value: "failed", label: `Failed (${counts.failed})` },
              { value: "missed", label: `Missed (${counts.missed})` },
              { value: "fp", label: `FP (${counts.fp})` },
              { value: "passed", label: `Passed (${counts.passed})` },
            ]}
          />
        </div>
        <FilterSearch
          id="benchmark-image-search"
          label="Search file or image ID"
          placeholder="frame_000000"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPageNumber(1);
          }}
        />
      </FilterBar>

      <TableScroller>
        <Table style={{ width: "100%" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Image ID</Table.Cell>
              <Table.Cell>File</Table.Cell>
              <Table.Cell>Status</Table.Cell>
              <Table.Cell>GT</Table.Cell>
              <Table.Cell>Pred</Table.Cell>
              <Table.Cell>Matched</Table.Cell>
              <Table.Cell>Missed</Table.Cell>
              <Table.Cell>FP</Table.Cell>
              <Table.Cell>Mean IoU</Table.Cell>
              <Table.Cell>Compare</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {pageItems.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={10}>
                  <Typography variant="body_short">No images match this filter.</Typography>
                </Table.Cell>
              </Table.Row>
            ) : (
              pageItems.map((row) => {
                const passed = isImagePassed(row);
                return (
                  <Table.Row
                    key={row.image_id}
                    onClick={() => onSelectImage(row.image_id)}
                    style={{ cursor: "pointer" }}
                  >
                    <Table.Cell>{row.image_id}</Table.Cell>
                    <Table.Cell>{row.file_name}</Table.Cell>
                    <Table.Cell>
                      <StatusChip status={passed ? "Succeeded" : "Failed"} />
                    </Table.Cell>
                    <Table.Cell>{row.gt_count}</Table.Cell>
                    <Table.Cell>{row.pred_count}</Table.Cell>
                    <Table.Cell>{row.matched}</Table.Cell>
                    <Table.Cell>{row.missed}</Table.Cell>
                    <Table.Cell>{row.false_positives}</Table.Cell>
                    <Table.Cell>{formatNumber(row.mean_iou, 3)}</Table.Cell>
                    <Table.Cell>
                      <Button
                        variant="ghost"
                        onClick={(e) => {
                          e.stopPropagation();
                          onSelectImage(row.image_id);
                        }}
                      >
                        Compare
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                );
              })
            )}
          </Table.Body>
        </Table>
      </TableScroller>

      <PaginationFooter
        hasResponse
        pageNumber={safePage}
        pageSize={pageSize}
        totalCount={filtered.length}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        disabled={false}
        loading={false}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setPageNumber(1);
        }}
        resetKey={`${filter}-${search}-${pageSize}-${safePage}`}
      />
    </DashboardPanel>
  );
}
