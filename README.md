# ⚖️ Odhikar with Legal Hub (অধিকার)

> **AI-Assisted Legal Aid, Fact Extraction, and Case Management Platform**  
> Supporting Bangladesh Bar Council & National Legal Aid Services Organization (NLASO) standards.

---

## 🌐 Live Deployments & Demos

| Service | Access Link | Description |
| :--- | :--- | :--- |
| 🏠 **Web Application (Vercel)** | [https://odhikar-legal-hub.vercel.app](https://odhikar-legal-hub.vercel.app) | Public portal & Citizen intake landing |
| ⚖️ **Citizen AI Legal Intake** | [https://odhikar-legal-hub.vercel.app/intake](https://odhikar-legal-hub.vercel.app/intake) | Multilingual voice & text legal interview |
| 🧑‍⚖️ **Paralegal Desk & Registry** | [https://odhikar-legal-hub.vercel.app/paralegal](https://odhikar-legal-hub.vercel.app/paralegal) | Executive table & audit workspace |
| ⚙️ **Backend API (Render)** | [https://odhikar-backend.onrender.com/api](https://odhikar-backend.onrender.com/api) | High-performance .NET 8 Web API |
| 📑 **Interactive Swagger Docs** | [https://odhikar-backend.onrender.com/swagger/index.html](https://odhikar-backend.onrender.com/swagger/index.html) | OpenAPI endpoints & contracts |

---

## 📁 Repository Structure

```text
Odhikar-with-legal-hub/
├── frontend/             # Angular 19 client application
│   ├── src/
│   │   ├── app/
│   │   │   ├── welcome/      # Citizen landing page
│   │   │   ├── intake/       # AI conversational legal intake & speech
│   │   │   ├── paralegal/    # Paralegal desk (Table overview & Workspace)
│   │   │   ├── auth/         # Verified advocate & paralegal credentials
│   │   │   └── core/         # API services, models & guards
│   ├── package.json
│   └── angular.json
├── backend/              # .NET 8 Web API & Clean Architecture
│   ├── Odhikar.Api/              # Controllers, endpoints & Swagger
│   ├── Odhikar.Application/      # DTOs, interfaces & business engines
│   ├── Odhikar.Domain/           # MongoDB domain models
│   ├── Odhikar.Infrastructure/   # AI services (Gemini/Groq), PDF & Mongo DB
│   └── Odhikar.sln
└── README.md
```

---

## 🚀 Getting Started Locally

### 1. Backend (.NET 8)

#### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/)
- MongoDB instance (Local or MongoDB Atlas)

#### Running the Backend
```bash
cd backend
dotnet restore
dotnet run --project Odhikar.Api
```
The API will start at `http://localhost:5000/api` with Swagger UI at `http://localhost:5000/swagger`.

---

### 2. Frontend (Angular 19)

#### Prerequisites
- [Node.js](https://nodejs.org/) (v18+ recommended)
- Angular CLI: `npm install -g @angular/cli`

#### Running the Frontend
```bash
cd frontend
npm install
npm start
```
The application will be accessible at `http://localhost:4200/`.

---

## 🛠️ Key Features

- **Executive Paralegal Table Registry**: Complete case management table with real-time searching, category filtering, urgent badges, checklist scores, and instant navigation.
- **Dedicated Case Audit Workspace**: Full-screen workspace with inline facts editing, judicial readiness checklist verification, voice recording playback, and legal document generator.
- **Official Legal Petition PDF Export**: Generates compliant court petitions and complaints with official letterheads and signatures.
- **Bilingual & Voice-Enabled**: Full support for Bengali and English with Speech-to-Text and Text-to-Speech audio integration.
