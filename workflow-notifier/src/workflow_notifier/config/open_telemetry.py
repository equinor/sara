from __future__ import annotations

import logging
from typing import Dict
from urllib.parse import urlparse

from opentelemetry import metrics, trace
from opentelemetry._logs import set_logger_provider
from opentelemetry.exporter.otlp.proto.grpc._log_exporter import (
    OTLPLogExporter as OTLPGrpcLogExporter,
)
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import (
    OTLPMetricExporter as OTLPGrpcMetricExporter,
)
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import (
    OTLPSpanExporter as OTLPGrpcSpanExporter,
)
from opentelemetry.exporter.otlp.proto.http._log_exporter import (
    OTLPLogExporter as OTLPHttpLogExporter,
)
from opentelemetry.exporter.otlp.proto.http.metric_exporter import (
    OTLPMetricExporter as OTLPHttpMetricExporter,
)
from opentelemetry.exporter.otlp.proto.http.trace_exporter import (
    OTLPSpanExporter as OTLPHttpSpanExporter,
)
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor, LogRecordExporter
from opentelemetry.sdk.metrics import (
    Counter as SdkCounter,
    Histogram as SdkHistogram,
    MeterProvider,
    ObservableCounter as SdkObservableCounter,
)
from opentelemetry.sdk.metrics.export import (
    AggregationTemporality,
    MetricExporter,
    PeriodicExportingMetricReader,
)
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor, SpanExporter

from workflow_notifier.config.settings import settings

logger = logging.getLogger(__name__)


def _normalize_grpc_endpoint(endpoint: str) -> str:
    """
    OTLP gRPC exporters typically expect "host:port" (no scheme).
    If a scheme is provided (e.g. http://localhost:4317), strip it.
    """
    if endpoint.startswith("http://") or endpoint.startswith("https://"):
        parsed = urlparse(endpoint)
        if parsed.hostname:
            port = parsed.port or 4317
            return f"{parsed.hostname}:{port}"
    return endpoint


def _preferred_temporality() -> Dict[type, AggregationTemporality]:
    pref = settings.OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE.strip().upper()

    if pref == "DELTA":
        return {
            SdkCounter: AggregationTemporality.DELTA,
            SdkObservableCounter: AggregationTemporality.DELTA,
            SdkHistogram: AggregationTemporality.DELTA,
        }

    if pref == "LOWMEMORY":
        return {
            SdkCounter: AggregationTemporality.DELTA,
            SdkHistogram: AggregationTemporality.DELTA,
            SdkObservableCounter: AggregationTemporality.CUMULATIVE,
        }

    if pref == "CUMULATIVE":
        return {
            SdkCounter: AggregationTemporality.CUMULATIVE,
            SdkObservableCounter: AggregationTemporality.CUMULATIVE,
            SdkHistogram: AggregationTemporality.CUMULATIVE,
        }

    logger.warning(
        "Unknown OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=%r; defaulting to DELTA",
        pref,
    )
    return {
        SdkCounter: AggregationTemporality.DELTA,
        SdkObservableCounter: AggregationTemporality.DELTA,
        SdkHistogram: AggregationTemporality.DELTA,
    }


def setup_open_telemetry() -> None:
    service_name = settings.OTEL_SERVICE_NAME
    endpoint = settings.OTEL_EXPORTER_OTLP_ENDPOINT
    protocol = settings.OTEL_EXPORTER_OTLP_PROTOCOL.strip().lower()

    preferred_temporality = _preferred_temporality()

    resource = Resource.create({"service.name": service_name})

    span_exporter: SpanExporter
    log_exporter: LogRecordExporter
    metric_exporter: MetricExporter

    if protocol == "http":
        base = endpoint.rstrip("/")
        span_exporter = OTLPHttpSpanExporter(endpoint=f"{base}/v1/traces")
        log_exporter = OTLPHttpLogExporter(endpoint=f"{base}/v1/logs")  # type: ignore
        metric_exporter = OTLPHttpMetricExporter(
            endpoint=f"{base}/v1/metrics",
            preferred_temporality=preferred_temporality,
        )
    elif protocol == "grpc":
        grpc_endpoint = _normalize_grpc_endpoint(endpoint)
        span_exporter = OTLPGrpcSpanExporter(endpoint=grpc_endpoint, insecure=True)
        log_exporter = OTLPGrpcLogExporter(endpoint=grpc_endpoint, insecure=True)  # type: ignore
        metric_exporter = OTLPGrpcMetricExporter(
            endpoint=grpc_endpoint,
            insecure=True,
            preferred_temporality=preferred_temporality,
        )
    else:
        raise ValueError(
            f"Unknown OTLP protocol: {protocol!r} (expected 'grpc' or 'http')"
        )

    # --- Traces ---
    tracer_provider = TracerProvider(resource=resource)
    tracer_provider.add_span_processor(BatchSpanProcessor(span_exporter))
    trace.set_tracer_provider(tracer_provider)

    # --- Logs ---
    log_provider = LoggerProvider(resource=resource)
    log_provider.add_log_record_processor(BatchLogRecordProcessor(log_exporter))
    set_logger_provider(log_provider)

    # --- Metrics ---
    reader = PeriodicExportingMetricReader(metric_exporter)
    meter_provider = MeterProvider(resource=resource, metric_readers=[reader])
    metrics.set_meter_provider(meter_provider)

    root_logger = logging.getLogger()
    root_logger.setLevel(logging.INFO)

    root_logger.addHandler(
        LoggingHandler(level=logging.INFO, logger_provider=log_provider)
    )

    logger.info(
        "Set up OpenTelemetry service=%s endpoint=%s protocol=%s temporality=%s",
        service_name,
        endpoint,
        protocol,
        {k.__name__: str(v) for k, v in preferred_temporality.items()},
    )
