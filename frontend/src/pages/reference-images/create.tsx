import { useCallback, useState } from "react";
import {
    Button,
    Typography,
    TextField,
    Icon,
} from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate, useSearchParams } from "react-router";
import styled from "styled-components";
import {
    createReferencePolygonMetadata,
    createReferencePolygonFromInspectionRecord,
    getInspectionRecordThermalImage,
    getInspectionRecordFencillaImageUrl,
    getThermalInspectionRecords,
    getFencillaInspectionRecords,
    AnalysisType,
    type ReferencePolygonMetadataInput,
    type InspectionRecord,
    type ThermalImageData,
} from "../../api/client";
import { useResourceMutation } from "../../api/queries";
import InspectionRecordSelector from "../../components/InspectionRecordSelector";
import ThermalPolygonDrawingEditor from "../../components/ThermalPolygonDrawingEditor";
import { FencillaPolygonDrawingEditor } from "../../components/FencillaImagePolygon";
import SegmentedToggle from "../../components/SegmentedToggle";

const StyledBackNavRowLg = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
`;

const StyledFormContainer = styled.div`
  max-width: 640px;
  display: flex;
  flex-direction: column;
  gap: 1rem;
`;

const StyledBlobSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
  border: 1px solid #dcdcdc;
  border-radius: 0.5rem;
`;

const StyledActionRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
`;

const StyledImageSection = styled.div`
  margin-top: 1rem;
  position: relative;
`;

const LoadingOverlay = styled.div`
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: rgba(255, 255, 255, 0.6);
  z-index: 1;
  border-radius: 0.25rem;
`;

Icon.add({ arrow_back });

const emptyForm: ReferencePolygonMetadataInput = {
    tagId: "",
    installationCode: "",
    inspectionDescription: "",
    referenceBlobStorageDirectory: {
        blobContainer: "",
        blobName: "",
    },
    polygon: [],
    sourceAnalysisType: AnalysisType.ThermalReading,
};

const SOURCE_ANALYSIS_TYPES: { value: AnalysisType; label: string }[] = [
    { value: AnalysisType.ThermalReading, label: "Thermal Reading" },
    { value: AnalysisType.Fencilla, label: "Fencilla" },
];

const CREATE_MODES: { value: "manual" | "inspection"; label: string }[] = [
    { value: "manual", label: "Manual blob path" },
    { value: "inspection", label: "From inspection record" },
];

export default function CreateReferencePolygonMetadataPage() {
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const fromInspection = searchParams.get("mode") === "inspection";
    const [form, setForm] = useState<ReferencePolygonMetadataInput>(emptyForm);
    const createMutation = useResourceMutation(createReferencePolygonMetadata);
    const createFromInspectionMutation = useResourceMutation(createReferencePolygonFromInspectionRecord);
    const creating = createMutation.isPending || createFromInspectionMutation.isPending;
    const [error, setError] = useState<string | null>(null);

    // Inspection record selection state
    const [selectedRecord, setSelectedRecord] = useState<InspectionRecord | null>(
        null
    );
    const [thermalImageData, setThermalImageData] =
        useState<ThermalImageData | null>(null);
    const [fencillaImageUrl, setFencillaImageUrl] = useState<string | null>(null);
    const [loadingImage, setLoadingImage] = useState(false);
    const [polygon, setPolygon] = useState<number[][]>([]);

    const navigateBack = () => navigate("/reference-images");

    const isThermal = form.sourceAnalysisType === AnalysisType.ThermalReading;

    const resetInspectionSelection = () => {
        setSelectedRecord(null);
        setThermalImageData(null);
        setFencillaImageUrl(null);
        setPolygon([]);
        setError(null);
    };

    const handleSourceAnalysisTypeChange = (sourceAnalysisType: AnalysisType) => {
        if (creating || sourceAnalysisType === form.sourceAnalysisType) return;
        setForm((prev) => ({ ...prev, sourceAnalysisType }));
        resetInspectionSelection();
    };

    const handleRecordSelect = useCallback(
        async (record: InspectionRecord) => {
            if (creating) return;
            setSelectedRecord(record);
            setPolygon([]);
            setThermalImageData(null);
            setFencillaImageUrl(null);
            setError(null);

            // Auto-fill metadata fields
            setForm((prev) => ({
                ...prev,
                tagId: record.tag ?? prev.tagId,
                installationCode: record.installationCode ?? prev.installationCode,
                inspectionDescription:
                    record.inspectionDescription ?? prev.inspectionDescription,
            }));

            setLoadingImage(true);
            try {
                if (isThermal) {
                    setThermalImageData(await getInspectionRecordThermalImage(record.id));
                } else {
                    setFencillaImageUrl(await getInspectionRecordFencillaImageUrl(record.id));
                }
            } catch (e) {
                setError(
                    e instanceof Error
                        ? e.message
                        : "Failed to load source image from inspection record"
                );
            } finally {
                setLoadingImage(false);
            }
        },
        [isThermal, creating]
    );

    const handleCreateManual = async () => {
        if (creating) return;
        setError(null);
        try {
            await createMutation.mutateAsync(form);
            navigateBack();
        } catch (e) {
            setError(
                e instanceof Error
                    ? e.message
                    : "Failed to create reference metadata"
            );
        }
    };

    const handleCreateFromInspection = async () => {
        if (creating || !selectedRecord) return;
        if (polygon.length < 3) {
            setError("Please draw a polygon on the source image before submitting.");
            return;
        }

        setError(null);
        try {
            const result = await createFromInspectionMutation.mutateAsync({
                inspectionRecordId: selectedRecord.id,
                tagId: form.tagId,
                installationCode: form.installationCode,
                inspectionDescription: form.inspectionDescription,
                polygon,
                sourceAnalysisType: form.sourceAnalysisType,
            });
            navigate(`/reference-images/${result.id}`);
        } catch (e) {
            setError(
                e instanceof Error
                    ? e.message
                    : "Failed to create reference metadata from inspection record"
            );
        }
    };

    const canSubmitFromInspection =
        selectedRecord !== null &&
        polygon.length >= 3 &&
        form.tagId.trim() !== "" &&
        form.installationCode.trim() !== "" &&
        form.inspectionDescription.trim() !== "";

    return (
        <div style={{ paddingTop: "1rem" }}>
            <StyledBackNavRowLg>
                <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back" disabled={creating}>
                    <Icon name="arrow_back" />
                </Button>
                <Typography variant="h3">Create Reference Metadata</Typography>
            </StyledBackNavRowLg>

            {error && (
                <Typography
                    variant="body_short"
                    style={{ marginBottom: "1rem", color: "#eb0000" }}
                >
                    {error}
                </Typography>
            )}

            <StyledFormContainer inert={creating}>
                <div>
                    <Typography variant="body_short_bold" style={{ marginBottom: "0.5rem" }}>
                        Source Analysis Type
                    </Typography>
                    <SegmentedToggle
                        options={SOURCE_ANALYSIS_TYPES}
                        value={form.sourceAnalysisType}
                        onChange={handleSourceAnalysisTypeChange}
                    />
                </div>

                <TextField
                    id="tagId"
                    label="Tag ID"
                    value={form.tagId}
                    onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                        setForm({ ...form, tagId: e.target.value })
                    }
                />
                <TextField
                    id="installationCode"
                    label="Installation Code"
                    value={form.installationCode}
                    onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                        setForm({ ...form, installationCode: e.target.value })
                    }
                />
                <TextField
                    id="inspectionDescription"
                    label="Inspection Description"
                    value={form.inspectionDescription}
                    onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                        setForm({ ...form, inspectionDescription: e.target.value })
                    }
                />

                <SegmentedToggle
                    options={CREATE_MODES}
                    value={fromInspection ? "inspection" : "manual"}
                    onChange={(mode) => {
                        if (creating) return;
                        if (mode === "inspection" && !fromInspection) {
                            setSearchParams({ mode: "inspection" });
                            setError(null);
                        } else if (mode === "manual" && fromInspection) {
                            setSearchParams({});
                            resetInspectionSelection();
                        }
                    }}
                />

                {!fromInspection && (
                    <>
                        <StyledBlobSection>
                            <Typography variant="h6">
                                Reference Blob Storage Directory
                            </Typography>
                            <TextField
                                id="blobContainer"
                                label="Blob Container"
                                value={form.referenceBlobStorageDirectory.blobContainer}
                                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                    setForm({
                                        ...form,
                                        referenceBlobStorageDirectory: {
                                            ...form.referenceBlobStorageDirectory,
                                            blobContainer: e.target.value,
                                        },
                                    })
                                }
                            />
                            <TextField
                                id="blobDirectory"
                                label="Blob Directory"
                                value={form.referenceBlobStorageDirectory.blobName}
                                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                    setForm({
                                        ...form,
                                        referenceBlobStorageDirectory: {
                                            ...form.referenceBlobStorageDirectory,
                                            blobName: e.target.value,
                                        },
                                    })
                                }
                            />
                        </StyledBlobSection>

                        <StyledActionRow>
                            <Button onClick={handleCreateManual} disabled={creating}>
                                {creating ? "Creating..." : "Create"}
                            </Button>
                            <Button variant="ghost" onClick={navigateBack} disabled={creating}>
                                Cancel
                            </Button>
                        </StyledActionRow>
                    </>
                )}

                {fromInspection && (
                    <>
                        {isThermal ? (
                            <InspectionRecordSelector
                                title="Select a thermal inspection record"
                                analysisType={AnalysisType.ThermalReading}
                                fetchRecords={getThermalInspectionRecords}
                                onSelect={handleRecordSelect}
                                selectedId={selectedRecord?.id}
                            />
                        ) : (
                            <InspectionRecordSelector
                                title="Select a fencilla inspection record"
                                analysisType={AnalysisType.Fencilla}
                                fetchRecords={getFencillaInspectionRecords}
                                onSelect={handleRecordSelect}
                                selectedId={selectedRecord?.id}
                            />
                        )}

                        {(thermalImageData || fencillaImageUrl) && (
                            <StyledImageSection>
                                {loadingImage && (
                                    <LoadingOverlay>
                                        <Typography variant="body_short">
                                            Loading source image...
                                        </Typography>
                                    </LoadingOverlay>
                                )}
                                {isThermal && thermalImageData ? (
                                    <ThermalPolygonDrawingEditor
                                        temperatures={thermalImageData.temperatures}
                                        width={thermalImageData.width}
                                        height={thermalImageData.height}
                                        minTemperature={thermalImageData.minTemperature}
                                        maxTemperature={thermalImageData.maxTemperature}
                                        onPolygonChange={setPolygon}
                                    />
                                ) : (
                                    fencillaImageUrl && (
                                        <FencillaPolygonDrawingEditor
                                            imageUrl={fencillaImageUrl}
                                            onPolygonChange={setPolygon}
                                        />
                                    )
                                )}
                            </StyledImageSection>
                        )}

                        {!thermalImageData && !fencillaImageUrl && loadingImage && (
                            <StyledImageSection>
                                <Typography variant="body_short">
                                    Loading source image...
                                </Typography>
                            </StyledImageSection>
                        )}

                        <StyledActionRow>
                            <Button
                                onClick={handleCreateFromInspection}
                                disabled={creating || !canSubmitFromInspection}
                            >
                                {creating ? "Creating..." : "Create"}
                            </Button>
                            <Button variant="ghost" onClick={navigateBack} disabled={creating}>
                                Cancel
                            </Button>
                        </StyledActionRow>
                    </>
                )}
            </StyledFormContainer>
        </div>
    );
}
