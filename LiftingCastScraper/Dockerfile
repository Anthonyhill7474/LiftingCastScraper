# ---- Base Python image ----
FROM python:3.12-slim

# ---- Install system dependencies required by Playwright ----
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget gnupg ca-certificates curl \
    libnss3 libnspr4 \
    libatk1.0-0 libatk-bridge2.0-0 \
    libcups2 libdrm2 \
    libxkbcommon0 libxcomposite1 libxdamage1 \
    libxrandr2 libxext6 libxfixes3 \
    libx11-6 libx11-xcb1 \
    libpango-1.0-0 libcairo2 \
    libasound2 libatspi2.0-0 \
    libgbm1 \
    && rm -rf /var/lib/apt/lists/*

# ---- App directory ----
WORKDIR /app

# ---- Install Python dependencies ----
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# ---- Install Playwright BROWSERS (Chromium only) ----
RUN playwright install chromium --with-deps

# ---- Copy application code ----
COPY src ./src

# Ensure Python can import src package
ENV PYTHONPATH=/app/src

# ---- Expose FastAPI port ----
EXPOSE 8000

# ---- Start FastAPI ----
CMD ["sh", "-c", "uvicorn liftingcastscraper.server.main:app --host 0.0.0.0 --port $PORT"]
