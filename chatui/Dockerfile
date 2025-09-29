# Stage 1: Build React app
FROM node:22 AS builder
WORKDIR /app
COPY . .
RUN npm install
RUN npm run build

# Stage 2: Copy static files to YARP container
FROM mcr.microsoft.com/dotnet/nightly/yarp:2.3.0-preview.4 AS yarp
WORKDIR /app
COPY --from=builder /app/dist ./wwwroot