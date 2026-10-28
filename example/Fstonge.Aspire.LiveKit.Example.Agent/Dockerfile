FROM python:3.11-slim

WORKDIR /app

# Install native dependencies for audio processing
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Run the LiveKit Agent worker
CMD ["python", "main.py", "start"]

