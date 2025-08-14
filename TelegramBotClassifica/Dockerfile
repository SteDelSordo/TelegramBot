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
# Il comando publish crea l'output nella cartella /app/publish
RUN dotnet publish -c Release -o /app/publish

# Fase 2: Creazione dell'immagine finale
# Usiamo l'immagine runtime, molto più leggera.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# CORREZIONE FINALE: Copiamo dalla cartella /app/publish creata nella fase di build.
COPY --from=build /app/publish .

# Comando di avvio.
ENTRYPOINT ["dotnet", "TelegramBotClassifica.dll"]
