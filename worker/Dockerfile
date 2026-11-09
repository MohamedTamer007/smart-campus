FROM python:3.13-slim-bookworm
WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 HF_HOME=/var/lib/smart-campus/model-cache
COPY worker/requirements.txt ./requirements.txt
# Select CPU wheels deliberately; GPU runtimes are unnecessary on the free CPU VM.
RUN pip install --no-cache-dir torch==2.15.0 --index-url https://download.pytorch.org/whl/cpu \
    && pip install --no-cache-dir -r requirements.txt
COPY worker/worker.py worker/verify_inference.py ./
RUN useradd --uid 1654 --create-home campus
USER 1654
ENTRYPOINT ["python", "worker.py", "--port", "8123"]
