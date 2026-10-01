# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DentalClinic.csproj ./
RUN dotnet restore DentalClinic.csproj
COPY . .
RUN dotnet publish DentalClinic.csproj -c Release -o /app --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# The connection string is supplied at run time (see docker-compose.yml).
ENV ASPNETCORE_URLS=http://+:8080 \
    Seed__DemoData=true
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DentalClinic.dll"]
