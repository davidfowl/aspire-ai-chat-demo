# Aspire AI Chat

Aspire AI Chat is a full-stack chat sample that combines modern technologies to deliver a ChatGPT-like experience.

## Architecture

```mermaid
graph TB
    User[User Browser]
    
    subgraph "YARP Reverse Proxy"
        YARP[chatui - YARP]
    end
    
    subgraph "Frontend"
        UI[React + TypeScript UI<br/>Vite Build]
    end
    
    subgraph "Backend API"
        API[ChatApi - ASP.NET Core<br/>SignalR Hub]
    end
    
    subgraph "Data Layer"
        PG[(PostgreSQL<br/>Conversation History)]
        Redis[(Redis<br/>Message Stream Cache)]
    end
    
    subgraph "AI Layer"
        LLM{AI Provider}
        Ollama[Ollama<br/>phi4 model<br/>Linux/Windows]
        OpenAI[OpenAI<br/>gpt-4.1<br/>macOS/Production]
    end
    
    User -->|HTTP/WS| YARP
    YARP -->|Static Files| UI
    YARP -->|/api/*| API
    
    API -->|SignalR| User
    API -->|EF Core| PG
    API -->|Pub/Sub| Redis
    API -->|IChatClient| LLM
    
    LLM -.->|Local| Ollama
    LLM -.->|Cloud| OpenAI
    
    style YARP fill:#0078d4,stroke:#004578,stroke-width:2px,color:#fff
    style API fill:#f37021,stroke:#b85419,stroke-width:2px,color:#fff
    style UI fill:#5c2d91,stroke:#3d1e61,stroke-width:2px,color:#fff
    style PG fill:#336791,stroke:#274466,stroke-width:2px,color:#fff
    style Redis fill:#dc382d,stroke:#a52a22,stroke-width:2px,color:#fff
    style LLM fill:#ffc107,stroke:#c79100,stroke-width:2px,color:#000
    style User fill:#2d333b,stroke:#1c2128,stroke-width:2px,color:#fff
    style Ollama fill:#4caf50,stroke:#388e3c,stroke-width:2px,color:#fff
    style OpenAI fill:#10a37f,stroke:#0d8267,stroke-width:2px,color:#fff
```

## High-Level Overview

- **Backend API:**  
  The backend is built with **ASP.NET Core** and interacts with an LLM using **Microsoft.Extensions.AI**. It leverages `IChatClient` to abstract the interaction between the API and the model. Chat responses are streamed back to the client using **SignalR** for real-time communication.

- **Data & Persistence:**  
  Uses **Entity Framework Core** with **PostgreSQL** for reliable relational data storage. **Redis** is used for caching and broadcasting live message streams across multiple clients.

- **AI & Chat Capabilities:**  
  - Uses **Ollama** (via OllamaSharp) for local inference on Linux/Windows.  
  - On macOS, the application uses [**OpenAI**](https://openai.com/) directly for better compatibility.
  - In production, the application can be configured to use various AI providers through the abstraction layer.

- **Frontend UI:**  
  Built with **React** and **TypeScript** using **Vite** for fast development and builds. The UI provides a modern chat interface with support for markdown rendering and conversation history.

- **Reverse Proxy & Serving:**  
  Uses **YARP** (Yet Another Reverse Proxy) to serve the static frontend and proxy API requests, providing a unified endpoint.

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started) or [Podman](https://podman-desktop.io/)
- [Node.js](https://nodejs.org/) (LTS version recommended)

### Running the Application

Run the application:

```bash
aspire run
```

This project uses [Aspire](https://aspire.dev) to orchestrate the application components in containers.

### Configuration

- By default, the application uses **Ollama** (phi4 model) for local inference on Linux/Windows.  
- On **macOS**, it automatically switches to **OpenAI** and will prompt for your OpenAI API key if not already configured.
- The **PostgreSQL** database and **Redis** cache are automatically provisioned when running with Aspire.
- Access the Aspire dashboard to monitor resources and view logs.

### Deployment

To deploy the application using Docker Compose:

```bash
aspire deploy
```

This generates a Docker Compose configuration in the `aspire-output` directory and runs the application stack. The deployment includes:

- All application services (ChatApi, UI)
- PostgreSQL database with persistent volume
- Redis cache
- Configured networking between services

The application will be accessible at the configured ports, with all services orchestrated through Docker Compose.

## CI/CD

The project uses **Aspire's pipeline system** to build and publish container images. The custom `push-gh` pipeline step (defined in `PipelineExtensions.cs`) handles:

- Building container images through Aspire's build pipeline
- Tagging images with format: `<branch>-<build-number>-<git-sha>`
- Pushing images to GitHub Container Registry (GHCR)

The GitHub Actions workflow invokes the pipeline with environment variables:

```yaml
- name: Push to GitHub Container Registry
  run: aspire do push-gh
  env:
    GHCR_REPO: ghcr.io/${{ github.repository_owner }}
    BRANCH_NAME: ${{ github.ref_name }}
    BUILD_NUMBER: ${{ github.run_number }}
    GIT_SHA: ${{ github.sha }}
```

The Aspire pipeline step reads these values, sanitizes the branch name for Docker compatibility, creates a semantic tag, and pushes the images to GHCR. This approach integrates seamlessly with Aspire's orchestration, allowing the AppHost to define both local development and CI/CD workflows in one place.

Images are available at: `ghcr.io/<owner>/chatapi` and `ghcr.io/<owner>/chatui`
