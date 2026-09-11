import { useCallback, useState } from "react";
import { Button, Typography, Icon } from "@equinor/eds-core-react";
import { arrow_back } from "@equinor/eds-icons";
import { useNavigate, useSearchParams } from "react-router";
import styled from "styled-components";
import {
    createReferencePolygonMetadata,
    createReferencePolygonFromInspectionRecord,
    getInspectionRecordThermalImage,
    getInspectionRecordFencillaImageUrl,
    AnalysisType,
    type ReferencePolygonMetadataInput,
    type InspectionRecord,
    type ThermalImageData,
} from "../../api/client";
import { useResourceMutation } from "../../api/queries";
import { ErrorText } from "../../components/Styles";
import CreateReferenceMetadataForm from "./reference-images-components/CreateReferenceMetadataForm";
import InspectionReferenceSourceSection from "./reference-images-components/InspectionReferenceSourceSection";

const StyledBackNavRowLg = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
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
                <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
                    {error}
                </ErrorText>
            )}

            <CreateReferenceMetadataForm
                form={form}
                creating={creating}
                fromInspection={fromInspection}
                canSubmitFromInspection={canSubmitFromInspection}
                onChange={setForm}
                onSourceAnalysisTypeChange={handleSourceAnalysisTypeChange}
                onModeChange={(mode) => {
                    if (creating) return;
                    if (mode === "inspection" && !fromInspection) {
                        setSearchParams({ mode: "inspection" });
                        setError(null);
                    } else if (mode === "manual" && fromInspection) {
                        setSearchParams({});
                        resetInspectionSelection();
                    }
                }}
                onCreateManual={handleCreateManual}
                onCreateFromInspection={handleCreateFromInspection}
                onCancel={navigateBack}
            >
                <InspectionReferenceSourceSection
                    isThermal={isThermal}
                    selectedRecord={selectedRecord}
                    thermalImageData={thermalImageData}
                    fencillaImageUrl={fencillaImageUrl}
                    loadingImage={loadingImage}
                    onRecordSelect={handleRecordSelect}
                    onPolygonChange={setPolygon}
                />
            </CreateReferenceMetadataForm>
        </div>
    );
}
