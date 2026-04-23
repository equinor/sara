import { NativeSelect, Pagination, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";

const StyledFooter = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.75rem 0.25rem;
  flex-wrap: wrap;
`;

interface PaginationFooterProps {
  hasResponse: boolean;
  pageNumber: number;
  pageSize: number;
  totalCount: number | null;
  pageSizeOptions?: number[];
  disabled?: boolean;
  loading?: boolean;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  resetKey?: string | number;
}

const DEFAULT_PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

export default function PaginationFooter({
  hasResponse,
  pageNumber,
  pageSize,
  totalCount,
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  disabled = false,
  loading = false,
  onPageChange,
  onPageSizeChange,
  resetKey,
}: PaginationFooterProps) {
  const start = totalCount === null || totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const end = totalCount === null ? 0 : Math.min(pageNumber * pageSize, totalCount);

  return (
    <StyledFooter>
      <div style={{ minWidth: "8rem" }}>
        <NativeSelect
          id="plant-data-page-size"
          label="Rows per page"
          value={String(pageSize)}
          disabled={disabled}
          onChange={(e) => onPageSizeChange(Number(e.target.value))}
        >
          {pageSizeOptions.map((opt) => (
            <option key={opt} value={opt}>
              {opt}
            </option>
          ))}
        </NativeSelect>
      </div>

      <Typography variant="body_short">
        {!hasResponse && loading
          ? "Loading results..."
          : totalCount === null
            ? ""
            : totalCount === 0
              ? "No results"
              : `Showing ${start.toLocaleString()}–${end.toLocaleString()} of ${totalCount.toLocaleString()}`}
      </Typography>

      <div
        style={{
          opacity: disabled ? 0.5 : 1,
          pointerEvents: disabled ? "none" : "auto",
        }}
      >
        {hasResponse && totalCount !== null && totalCount > 0 && (
          <Pagination
            key={resetKey ?? `${pageSize}`}
            totalItems={totalCount}
            itemsPerPage={pageSize}
            defaultPage={pageNumber}
            onChange={(_e, page) => onPageChange(page)}
          />
        )}
      </div>
    </StyledFooter>
  );
}
