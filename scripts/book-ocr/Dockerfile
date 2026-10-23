# Book OCR toolchain: scanned Vietnamese PDF -> Markdown drafts.
# CPU only. Kept out of the .NET build so `make lint` never touches it.
FROM python:3.12-slim-bookworm

ENV DEBIAN_FRONTEND=noninteractive \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1

RUN apt-get update && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-vie \
        poppler-utils \
        libgl1 \
        libglib2.0-0 \
        libgomp1 \
        libsm6 \
        libxext6 \
        libxrender1 \
        ca-certificates \
        curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip uv \
    && uv pip install --system --no-cache -r requirements.txt

COPY src ./src

RUN mkdir -p /work /models /input

ENV BOOK_OCR_INPUT=/input/book.pdf \
    BOOK_OCR_WORK=/work \
    BOOK_OCR_MODELS=/models \
    GEMINI_MODEL=gemini-3.5-flash \
    GEMINI_FALLBACK_MODELS=gemini-3.5-flash-lite,gemini-3.6-flash,gemini-3.7-flash,gemini-3.8-flash \
    VISION_WORKERS=2 \
    VISION_BATCH_SIZE=3

ENTRYPOINT ["python", "-m", "src.cli"]
CMD ["--help"]

