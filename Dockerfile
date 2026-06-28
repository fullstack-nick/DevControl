FROM node:22-alpine AS web-build
WORKDIR /src/DevControl.Web
COPY src/DevControl.Web/package*.json ./
RUN npm ci
COPY src/DevControl.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY . .
COPY --from=web-build /src/DevControl.Web/dist ./src/DevControl.Api/wwwroot
RUN dotnet restore DevControl.sln
RUN dotnet publish src/DevControl.Api/DevControl.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=dotnet-build /app/publish .
ENTRYPOINT ["dotnet", "DevControl.Api.dll"]

