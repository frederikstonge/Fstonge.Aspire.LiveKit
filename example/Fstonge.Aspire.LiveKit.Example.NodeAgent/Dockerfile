FROM node:20-slim

WORKDIR /app

# Install ca-certificates and curl for downloading dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY package*.json ./
RUN npm install

COPY . .

CMD ["npm", "start"]
