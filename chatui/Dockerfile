# Build Stage: Build the React app
FROM node:22 AS builder
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm install
COPY . .
RUN npm run build

# Production Stage: Serve the app with Caddy
FROM caddy:2.9-alpine
WORKDIR /usr/share/caddy

# Copy built static files including index.html, etc.
COPY --from=builder /app/build/ .

# Copy the Caddyfile with reverse proxy config
COPY Caddyfile /etc/caddy/Caddyfile

EXPOSE 80

# Run Caddy
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]