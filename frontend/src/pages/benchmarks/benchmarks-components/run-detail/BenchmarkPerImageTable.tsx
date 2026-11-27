import { useMemo, useState } from "react";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import type {
  BenchmarkImageResult,
  BenchmarkPerImageColumn,
} from "../../../../api/benchmarkClient";
import DashboardPanel from "../../../../components/DashboardPanel";
import PaginationFooter from "../../../../components/PaginationFooter";
import SegmentedToggle from "../../../../components/SegmentedToggle";
import StatusChip from "../../../../components/StatusChip";
import { FilterBar, FilterSearch, TableScroller } from "../../../../components/Styles";
import { formatMetricValue } from "../benchmarkFormatting";

type ImageOutcomeFilter = "all" | "failed" | "missed" | "fp" | "passed";

const PAGE_SIZE_OPTIONS = [25, 50, 100];

interface BenchmarkPerImageTableProps {
  columns: BenchmarkPerImageColumn[];
  items: BenchmarkImageResult[];
  onSelectImage: (imageId: number) => void;
}

export default function BenchmarkPerImageTable({
  columns,
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
      if (row.passed) passed += 1;
      else failed += 1;
      if (row.missed) missed += 1;
      if (row.false_positive) fp += 1;
    }
    return { all: items.length, failed, missed, fp, passed };
  }, [items]);

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    return items.filter((row) => {
      if (filter === "failed" && row.passed) return false;
      if (filter === "passed" && !row.passed) return false;
      if (filter === "missed" && !row.missed) return false;
      if (filter === "fp" && !row.false_positive) return false;
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
  const totalColumns = 4 + columns.length;

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
          placeholder="Search filename or ID"
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
              {columns.map((col) => (
                <Table.Cell key={col.key}>{col.label}</Table.Cell>
              ))}
              <Table.Cell>Compare</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {pageItems.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={totalColumns}>
                  <Typography variant="body_short">No images match this filter.</Typography>
                </Table.Cell>
              </Table.Row>
            ) : (
              pageItems.map((row) => (
                <Table.Row
                  key={row.image_id}
                  onClick={() => onSelectImage(row.image_id)}
                  style={{ cursor: "pointer" }}
                >
                  <Table.Cell>{row.image_id}</Table.Cell>
                  <Table.Cell>{row.file_name}</Table.Cell>
                  <Table.Cell>
                    <StatusChip status={row.passed ? "Succeeded" : "Failed"} />
                  </Table.Cell>
                  {columns.map((col) => (
                    <Table.Cell key={col.key}>
                      {formatMetricValue(row.values[col.key], col.format)}
                    </Table.Cell>
                  ))}
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
              ))
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
