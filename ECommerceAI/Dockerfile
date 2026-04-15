# Build from repo folder BE/E-Commerce-BE:
#   docker build -f ECommerceAI/Dockerfile -t ecommerce-ai .
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ECommerceAI/ECommerceAI.csproj", "ECommerceAI/"]
RUN dotnet restore "ECommerceAI/ECommerceAI.csproj"
COPY ECommerceAI/ ECommerceAI/
WORKDIR /src/ECommerceAI
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ECommerceAI.dll"]
