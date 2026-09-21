import { Button, Table, Tooltip, Typography } from "@equinor/eds-core-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import type { AnalysisResultValue, ResultSeverity } from "../../../api/client";
import { acknowledgeAlarm } from "../../../api/client";
import { activeAlarmsKey, useActiveAlarms } from "../../../api/dashboardQueries";
import DashboardPanel from "../../../components/DashboardPanel";
import { ErrorText, statusColors } from "../../../components/Styles";
import OverviewDenseTable from "./OverviewDenseTable";

function severityColor(severity: ResultSeverity) {
  return severity === "Alert" ? statusColors.error : statusColors.warning;
}

/** Renders whichever of the typed value columns this measurement actually uses. */
function formatValue(alarm: AnalysisResultValue) {
  if (alarm.valueKind === "Boolean") return alarm.booleanValue ? "Yes" : "No";
  if (alarm.valueKind === "Numeric" && alarm.numericValue != null) {
    const rounded = Number(alarm.numericValue.toFixed(2));
    return alarm.unit ? `${rounded} ${alarm.unit}` : String(rounded);
  }
  return alarm.textValue ?? "—";
}

export default function ActiveAlarmsPanel() {
  const queryClient = useQueryClient();
  const { data: alarms, error, isPending } = useActiveAlarms();
  const [acknowledging, setAcknowledging] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const handleAcknowledge = async (alarm: AnalysisResultValue) => {
    const label = alarm.tag ?? alarm.installationCode;
    if (!window.confirm(`Mark "${alarm.key}" on ${label} as checked out?`)) return;

    setAcknowledging(alarm.id);
    setActionError(null);
    try {
      await acknowledgeAlarm(alarm.id);
      await queryClient.invalidateQueries({ queryKey: activeAlarmsKey });
    } catch (e) {
      setActionError(e instanceof Error ? e.message : "Could not check out the alarm");
    } finally {
      setAcknowledging(null);
    }
  };

  const count = alarms?.length ?? 0;
  const hasAlert = alarms?.some((alarm) => alarm.severity === "Alert") ?? false;

  return (
    <DashboardPanel
      title={`Active alarms${count > 0 ? ` (${count})` : ""}`}
      accent={hasAlert ? statusColors.error.accent : statusColors.warning.accent}
    >
      {actionError && (
        <ErrorText variant="body_short" role="alert">
          {actionError}
        </ErrorText>
      )}
      {error && (
        <ErrorText variant="body_short" role="alert">
          {error.message}
        </ErrorText>
      )}
      {isPending && <Typography variant="body_short">Loading alarms…</Typography>}
      {!isPending && !error && count === 0 && (
        <Typography variant="body_short">No active alarms.</Typography>
      )}
      {count > 0 && (
        <OverviewDenseTable>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Tag</Table.Cell>
              <Table.Cell>Finding</Table.Cell>
              <Table.Cell>Value</Table.Cell>
              <Table.Cell>Severity</Table.Cell>
              <Table.Cell>Measured</Table.Cell>
              <Table.Cell />
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {alarms!.map((alarm) => (
              <Table.Row key={alarm.id}>
                <Table.Cell>
                  {alarm.tag ?? alarm.installationCode}
                  {alarm.inspectionDescription && (
                    <Typography variant="caption" as="div">
                      {alarm.inspectionDescription}
                    </Typography>
                  )}
                </Table.Cell>
                <Table.Cell>
                  {alarm.key}
                  {alarm.modelMessage && (
                    <Typography variant="caption" as="div">
                      {alarm.modelMessage}
                    </Typography>
                  )}
                </Table.Cell>
                <Table.Cell>{formatValue(alarm)}</Table.Cell>
                <Table.Cell style={{ color: severityColor(alarm.severity).text }}>
                  {alarm.severity}
                </Table.Cell>
                <Table.Cell>
                  <Tooltip title={new Date(alarm.measuredAt).toLocaleString()}>
                    <span>{new Date(alarm.measuredAt).toLocaleDateString()}</span>
                  </Tooltip>
                </Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    disabled={acknowledging !== null}
                    onClick={() => handleAcknowledge(alarm)}
                  >
                    {acknowledging === alarm.id ? "Checking out…" : "Check out"}
                  </Button>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </OverviewDenseTable>
      )}
    </DashboardPanel>
  );
}
