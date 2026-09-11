import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Pagination, Table, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { AnalysisType, InspectionRecord, PagedResponse } from "../api/client";
import { ErrorText, TableScroller } from "./Styles";

export interface InspectionRecordSelectorProps {
    title: string;
    analysisType: AnalysisType;
    fetchRecords: (pageNumber: number, pageSize: number, signal?: AbortSignal) => Promise<PagedResponse<InspectionRecord>>;
    onSelect: (record: InspectionRecord) => void;
    selectedId?: string;
}

const SelectorContainer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`;

export default function InspectionRecordSelector({
    title,
    analysisType,
    fetchRecords,
    onSelect,
    selectedId,
}: InspectionRecordSelectorProps) {
    const [page, setPage] = useState(1);
    const pageSize = 20;
    const { data, isPending: loading, error } = useQuery({
        queryKey: ["sara", "inspection-selector", { analysisType, pageNumber: page, pageSize }],
        queryFn: ({ signal }) => fetchRecords(page, pageSize, signal),
    });

    return (
        <SelectorContainer>
            <Typography variant="h6">{title}</Typography>

            {error && (
                <ErrorText variant="body_short">
                    {error instanceof Error ? error.message : "Failed to fetch inspection records"}
                </ErrorText>
            )}

            {loading ? (
                <Typography variant="body_short">Loading...</Typography>
            ) : data && data.items.length > 0 ? (
                <>
                    <TableScroller>
                        <Table>
                            <Table.Head>
                                <Table.Row>
                                    <Table.Cell>Tag</Table.Cell>
                                    <Table.Cell>Installation</Table.Cell>
                                    <Table.Cell>Description</Table.Cell>
                                    <Table.Cell>Timestamp</Table.Cell>
                                </Table.Row>
                            </Table.Head>
                            <Table.Body>
                                {data.items.map((record) => (
                                    <Table.Row
                                        key={record.id}
                                        onClick={() => onSelect(record)}
                                        style={{
                                            cursor: "pointer",
                                            backgroundColor:
                                                selectedId === record.id ? "#e6faec" : undefined,
                                        }}
                                    >
                                        <Table.Cell>{record.tag ?? "-"}</Table.Cell>
                                        <Table.Cell>{record.installationCode}</Table.Cell>
                                        <Table.Cell>
                                            {record.inspectionDescription ?? "-"}
                                        </Table.Cell>
                                        <Table.Cell>
                                            {record.timestamp
                                                ? new Date(record.timestamp).toLocaleString()
                                                : record.createdAt
                                                    ? new Date(record.createdAt).toLocaleString()
                                                    : "-"}
                                        </Table.Cell>
                                    </Table.Row>
                                ))}
                            </Table.Body>
                        </Table>
                    </TableScroller>

                    <Pagination
                        totalItems={data.totalCount}
                        itemsPerPage={data.pageSize}
                        defaultPage={page}
                        onChange={(_, newPage) => setPage(newPage)}
                    />
                </>
            ) : (
                <Typography variant="body_short">
                    No inspection records found.
                </Typography>
            )}
        </SelectorContainer>
    );
}
