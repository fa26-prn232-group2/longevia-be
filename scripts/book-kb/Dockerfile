# Book knowledge-base pipeline: Markdown corpus -> chunked, embedded chunks.
# CPU only. Kept out of the .NET build so `make lint` never touches it.
FROM python:3.12-slim-bookworm

ENV DEBIAN_FRONTEND=noninteractive \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

COPY requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip uv \
    && uv pip install --system --no-cache -r requirements.txt

COPY src ./src
COPY tests ./tests

RUN mkdir -p /work

ENV BOOK_KB_WORK=/work \
    BOOK_KB_CORPUS=/corpus

ENTRYPOINT ["python", "-m", "src.cli"]
CMD ["--help"]
