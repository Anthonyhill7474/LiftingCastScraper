# Use an official lightweight Python runtime
FROM python:3.12-slim

# Set work directory
WORKDIR /app

# System deps – you may need more later for Chrome/Selenium,
# but this keeps it minimal for now.
RUN apt-get update && apt-get install -y \
    gcc \
    libxml2-dev \
    libxslt1-dev \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy your app code
COPY src ./src

# Make sure Python can see src/
ENV PYTHONPATH=/app/src

# Expose port (Render will use this)
EXPOSE 8000

# Start FastAPI using uvicorn
CMD ["uvicorn", "liftingcastscraper.server.main:app", "--host", "0.0.0.0", "--port", "8000"]
