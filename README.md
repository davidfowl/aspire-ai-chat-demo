# Aspire AI Chat

Aspire AI Chat is a full-stack chat sample that combines modern technologies to deliver a ChatGPT-like experience.

## High-Level Overview

- **Backend API:**  
  The backend is built with **ASP.NET Core** and interacts with an LLM using **Microsoft.Extensions.AI**. It leverages `IChatClient` to abstract the interaction between the API and the model. Chat responses are streamed back to the client using stream JSON array responses.

- **Data & Persistence:**  
  Uses **Entity Framework Core** with **PostgreSQL** for reliable relational data storage. The project includes database migrations for easy setup and version control of the schema.

- **AI & Chat Capabilities:**  
  - Uses **Ollama** (via OllamaSharp) for local inference, enabling context-aware responses.  
  - In production, the application switches to [**OpenAI**](https://openai.com/) for LLM capabilities.

- **Frontend UI:**  
  Built with **React**, the user interface offers a modern and interactive chat experience. The React application is built and hosted using [**Caddy**](https://caddyserver.com/).

## Getting Started

### Prerequisites

- [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [Docker](https://www.docker.com/get-started) or [Podman](https://podman-desktop.io/)
- [Node.js](https://nodejs.org/) (LTS version recommended)

### Running the Application

Run the [AIChat.AppHost](AIChat.AppHost) project. This project uses  
[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview)  
to run the application in a container.

### Configuration

- By default, the application uses **Ollama** for local inference.  
- To use **OpenAI**, set the appropriate configuration values (e.g., API keys, endpoints).  
- The PostgreSQL database will be automatically created and migrated when running with Aspire.

