# Odhikar with Legal Hub

A full-stack AI-assisted legal aid and paralegal support platform designed to streamline intake, legal fact extraction, and case management.

---

## 📁 Repository Structure

```text
Odhikar-with-legal-hub/
├── frontend/             # Angular client application
│   ├── src/
│   ├── package.json
│   └── angular.json
├── backend/              # .NET Core web API & services
│   ├── Odhikar.Api/
│   ├── Odhikar.Application/
│   ├── Odhikar.Domain/
│   ├── Odhikar.Infrastructure/
│   └── Odhikar.sln
└── README.md
```

---

## 🚀 Getting Started

### 1. Backend (.NET)

#### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/)
- MongoDB instance (local or Atlas)

#### Running the Backend
```bash
cd backend
dotnet restore
dotnet run --project Odhikar.Api
```
The API will start with Swagger documentation enabled at `http://localhost:5000/swagger` (or configured port).

---

### 2. Frontend (Angular)

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

## 🛠️ Tech Stack

- **Frontend**: Angular, TypeScript, HTML5/CSS3
- **Backend**: .NET 8, C#, Clean Architecture (Api, Application, Domain, Infrastructure)
- **Database**: MongoDB
- **AI Integrations**: Gemini, OpenRouter, Groq Whisper
