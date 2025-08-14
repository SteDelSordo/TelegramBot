# Fase 1: Build dell'applicazione
# Usiamo l'SDK di .NET 8 per avere tutti gli strumenti di compilazione.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Dato che la "Root Directory" su Render è impostata, il nostro contesto
# di build è già DENTRO la cartella "TelegramBotClassifica".

# Copia prima il file .csproj per sfruttare la cache di Docker.
COPY *.csproj .
RUN dotnet restore

# Ora copia tutto il resto (codice, file json, etc.)
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Fase 2: Creazione dell'immagine finale
# Usiamo l'immagine runtime, molto più leggera.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copia solo l'applicazione pubblicata dalla fase di build.
COPY --from=build /src/app/publish .

# Comando di avvio.
ENTRYPOINT ["dotnet", "TelegramBotClassifica.dll"]
