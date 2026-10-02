# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/DentalClinic/DentalClinic.csproj src/DentalClinic/
RUN dotnet restore src/DentalClinic/DentalClinic.csproj
COPY src/DentalClinic src/DentalClinic
RUN dotnet publish src/DentalClinic/DentalClinic.csproj -c Release -o /app --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# The connection string is supplied at run time (see docker-compose.yml).
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DentalClinic.dll"]
