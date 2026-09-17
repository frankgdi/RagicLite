FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TableMint.slnx ./
COPY TableMint.Domain/TableMint.Domain.csproj TableMint.Domain/
COPY TableMint.Application/TableMint.Application.csproj TableMint.Application/
COPY TableMint.Infrastructure/TableMint.Infrastructure.csproj TableMint.Infrastructure/
COPY TableMint.WebApi/TableMint.WebApi.csproj TableMint.WebApi/
RUN dotnet restore TableMint.WebApi/TableMint.WebApi.csproj

COPY TableMint.Domain/ TableMint.Domain/
COPY TableMint.Application/ TableMint.Application/
COPY TableMint.Infrastructure/ TableMint.Infrastructure/
COPY TableMint.WebApi/ TableMint.WebApi/
RUN dotnet publish TableMint.WebApi/TableMint.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "TableMint.WebApi.dll"]
