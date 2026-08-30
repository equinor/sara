FROM python:3.14-slim@sha256:ce40764625a4ff50df3548277632e7f96c4e77fe75fa848aae9885476e7df5a4

WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY . .

RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    libpq-dev \
    && pip install --upgrade pip \
    && pip install . \
    && apt-get remove -y gcc libpq-dev \
    && apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN chown -R appuser:appuser /app

USER appuser

CMD ["python", "main.py"]
