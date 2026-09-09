import { useCallback, useEffect, useState } from "react";
import { Pagination, Table, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { InspectionRecord, PagedResponse } from "../api/client";

export interface InspectionRecordSelectorProps {
    title: string;
    fetchRecords: (pageNumber: number) => Promise<PagedResponse<InspectionRecord>>;
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
    fetchRecords,
    onSelect,
    selectedId,
}: InspectionRecordSelectorProps) {
    const [page, setPage] = useState(1);
    const [data, setData] = useState<PagedResponse<InspectionRecord> | null>(
        null
    );
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(
        async (pageNumber: number) => {
            setLoading(true);
            setError(null);
            try {
                const result = await fetchRecords(pageNumber);
                setData(result);
            } catch (e) {
                setError(
                    e instanceof Error ? e.message : "Failed to fetch inspection records"
                );
            } finally {
                setLoading(false);
            }
        },
        [fetchRecords]
    );

    useEffect(() => {
        load(page);
    }, [load, page]);

    return (
        <SelectorContainer>
            <Typography variant="h6">{title}</Typography>

            {error && (
                <Typography variant="body_short" style={{ color: "#eb0000" }}>
                    {error}
                </Typography>
            )}

            {loading ? (
                <Typography variant="body_short">Loading...</Typography>
            ) : data && data.items.length > 0 ? (
                <>
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
