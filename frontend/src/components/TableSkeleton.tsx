import { Table } from "@equinor/eds-core-react";

interface TableSkeletonProps {
  columns: number;
  rows?: number;
}

export default function TableSkeleton({ columns, rows = 10 }: TableSkeletonProps) {
  return (
    <>
      {Array.from({ length: rows }).map((_, rowIdx) => (
        <Table.Row key={rowIdx}>
          {Array.from({ length: columns }).map((__, colIdx) => (
            <Table.Cell key={colIdx}>
              <div
                style={{
                  height: "1rem",
                  width: `${40 + ((rowIdx * 7 + colIdx * 13) % 50)}%`,
                  background:
                    "linear-gradient(90deg, #e6e6e6 0%, #f5f5f5 50%, #e6e6e6 100%)",
                  backgroundSize: "200% 100%",
                  borderRadius: "4px",
                  animation: "skeleton-shimmer 1.2s ease-in-out infinite",
                }}
              />
            </Table.Cell>
          ))}
        </Table.Row>
      ))}
      <style>{`
        @keyframes skeleton-shimmer {
          0% { background-position: 200% 0; }
          100% { background-position: -200% 0; }
        }
      `}</style>
    </>
  );
}
