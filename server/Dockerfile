# ---- Stage 1: build. A big image with the compiler, thrown away afterwards ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first, so downloading packages is cached between builds
COPY TaskHub.Core/TaskHub.Core.csproj TaskHub.Core/
COPY TaskHub.Infrastructure/TaskHub.Infrastructure.csproj TaskHub.Infrastructure/
COPY TaskHub.Background/TaskHub.Background.csproj TaskHub.Background/
COPY TaskHub.Api/TaskHub.Api.csproj TaskHub.Api/
RUN dotnet restore TaskHub.Api/TaskHub.Api.csproj

# Now copy the code and build it
COPY TaskHub.Core/ TaskHub.Core/
COPY TaskHub.Infrastructure/ TaskHub.Infrastructure/
COPY TaskHub.Background/ TaskHub.Background/
COPY TaskHub.Api/ TaskHub.Api/
RUN dotnet publish TaskHub.Api/TaskHub.Api.csproj -c Release -o /app --no-restore

# ---- Stage 2: run. A small image with only what is needed to run the app ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
ENTRYPOINT ["dotnet", "TaskHub.Api.dll"]