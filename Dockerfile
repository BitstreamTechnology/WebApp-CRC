
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

EXPOSE 80

WORKDIR /src

COPY CRC-Calculator.csproj .

RUN dotnet restore CRC-Calculator.csproj

COPY . .

RUN dotnet build CRC-Calculator.csproj -c Release -o /app/build

FROM build AS publish

RUN dotnet publish CRC-Calculator.csproj -c Release -o /app/publish

FROM nginx:alpine AS final

WORKDIR /usr/share/nginx/html

COPY --from=publish /app/publish/wwwroot .

COPY nginx.conf /etc/nginx/nginx.conf

COPY mime.types /etc/nginx/mime.types
