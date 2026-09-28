# Stage 1: Build & Publish .NET 9 Web API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Odhikar.Domain/Odhikar.Domain.csproj", "Odhikar.Domain/"]
COPY ["Odhikar.Application/Odhikar.Application.csproj", "Odhikar.Application/"]
COPY ["Odhikar.Infrastructure/Odhikar.Infrastructure.csproj", "Odhikar.Infrastructure/"]
COPY ["Odhikar.Api/Odhikar.Api.csproj", "Odhikar.Api/"]
RUN dotnet restore "Odhikar.Api/Odhikar.Api.csproj"

COPY . .
WORKDIR "/src/Odhikar.Api"
RUN dotnet publish "Odhikar.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: ASP.NET Core 9.0 Runtime with Bangla font support for QuestPDF
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install fontconfig and Bengali fonts so QuestPDF renders Bengali properly
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    libfontconfig1 \
    libfreetype6 \
    fonts-beng \
    fonts-beng-extra \
    fonts-lohit-beng-bengali \
    curl \
    && fc-cache -f -v \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Odhikar.Api.dll"]
