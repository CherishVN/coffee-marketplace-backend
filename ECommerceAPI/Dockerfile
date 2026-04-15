# Build from repo folder BE/E-Commerce-BE:
#   docker build -f ECommerceAPI/Dockerfile -t ecommerce-api .FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ECommerceAPI/ECommerceAPI.csproj", "ECommerceAPI/"]
RUN dotnet restore "ECommerceAPI/ECommerceAPI.csproj"
COPY ECommerceAPI/ ECommerceAPI/
WORKDIR /src/ECommerceAPI
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ECommerceAPI.dll"]
