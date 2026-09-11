import { Button, Table, Typography } from "@equinor/eds-core-react"
import styled from "styled-components"
import { AnalysisType, type ReferencePolygonMetadata } from "../../../api/client"
import IdCell from "../../../components/IdCell"
import { TableScroller } from "../../../components/Styles"

const StyledActions = styled.div`
  display: flex;
  gap: 0.5rem;
`

const StyledTable = styled(Table)`
  width: 1200px;
`

const SOURCE_ANALYSIS_TYPE_LABELS: Record<AnalysisType, string> = {
    [AnalysisType.ThermalReading]: "Thermal Reading",
    [AnalysisType.Fencilla]: "Fencilla",
    [AnalysisType.CLOE]: "CLOE",
    [AnalysisType.CO2]: "CO2",
}

interface Props {
    data: ReferencePolygonMetadata[]
    deleting: boolean
    onSelect: (id: string) => void
    onDelete: (id: string) => void
}

export default function ReferenceMetadataTable({ data, deleting, onSelect, onDelete }: Props) {
    return (
        <TableScroller>
            <StyledTable>
                <Table.Head>
                    <Table.Row>
                        <Table.Cell>ID</Table.Cell>
                        <Table.Cell>Installation</Table.Cell>
                        <Table.Cell>Tag</Table.Cell>
                        <Table.Cell>Inspection Description</Table.Cell>
                        <Table.Cell>Source Type</Table.Cell>
                        <Table.Cell>Actions</Table.Cell>
                    </Table.Row>
                </Table.Head>
                <Table.Body>
                    {data.map((metadata) => (
                        <Table.Row
                            key={metadata.id}
                            onClick={() => onSelect(metadata.id)}
                            style={{ cursor: "pointer" }}
                        >
                            <Table.Cell>
                                <IdCell id={metadata.id} />
                            </Table.Cell>
                            <Table.Cell>{metadata.installationCode.toUpperCase()}</Table.Cell>
                            <Table.Cell>{metadata.tagId}</Table.Cell>
                            <Table.Cell>{metadata.inspectionDescription}</Table.Cell>
                            <Table.Cell>
                                {SOURCE_ANALYSIS_TYPE_LABELS[metadata.sourceAnalysisType]}
                            </Table.Cell>
                            <Table.Cell>
                                <StyledActions>
                                    <Button
                                        variant="ghost"
                                        color="danger"
                                        disabled={deleting}
                                        onClick={(event) => {
                                            event.stopPropagation()
                                            onDelete(metadata.id)
                                        }}
                                    >
                                        Delete
                                    </Button>
                                </StyledActions>
                            </Table.Cell>
                        </Table.Row>
                    ))}
                    {data.length === 0 && (
                        <Table.Row>
                            <Table.Cell colSpan={6}>
                                <Typography variant="body_short">
                                    No reference metadata found.
                                </Typography>
                            </Table.Cell>
                        </Table.Row>
                    )}
                </Table.Body>
            </StyledTable>
        </TableScroller>
    )
}
