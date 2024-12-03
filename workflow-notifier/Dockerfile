FROM python:3.12-slim


WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY ./requirements.txt /app/requirements.txt

RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    libpq-dev \
    && pip install --upgrade pip \
    && pip install -r requirements.txt \
    && apt-get remove -y gcc libpq-dev \
    && apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY ./main.py /app/main.py

RUN chown -R appuser:appuser /app

USER appuser

CMD ["python", "main.py"]
