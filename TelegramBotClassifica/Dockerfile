# Fase 1: Build dell'applicazione
# Usiamo l'SDK di .NET 8 per avere tutti gli strumenti di compilazione.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia il file di progetto (.csproj) e ripristina i pacchetti NuGet.
# Questo strato viene messo in cache da Docker, velocizzando le build future se le dipendenze non cambiano.
COPY TelegramBotClassifica.csproj .
RUN dotnet restore "TelegramBotClassifica.csproj"

# Copia tutto il resto del codice sorgente e dei file (incluso users_backup.json)
COPY . .
RUN dotnet publish "TelegramBotClassifica.csproj" -c Release -o /app/publish

# Fase 2: Creazione dell'immagine finale
# Usiamo l'immagine runtime di ASP.NET, molto più leggera dell'SDK, per l'esecuzione.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copia solo l'applicazione pubblicata dalla fase di build.
COPY --from=build /app/publish .

# Comando che verrà eseguito all'avvio del container.
ENTRYPOINT ["dotnet", "TelegramBotClassifica.dll"]
