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

# SQLite database lives in a volume so data survives restarts.
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__DefaultConnection="Data Source=/data/clinic.db" \
    Seed__DemoData=true
RUN mkdir /data && chown -R $APP_UID /data
VOLUME /data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DentalClinic.dll"]
