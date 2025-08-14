# Fase 1: Build dell'applicazione
# Usiamo l'SDK di .NET 8 per avere tutti gli strumenti di compilazione.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# CORREZIONE #1: Specifichiamo il percorso completo del file .csproj
# partendo dalla root del repository.
COPY ["TelegramBotClassifica/TelegramBotClassifica.csproj", "TelegramBotClassifica/"]
RUN dotnet restore "TelegramBotClassifica/TelegramBotClassifica.csproj"

# CORREZIONE #2: Copiamo il contenuto della sottocartella nella sua controparte
# all'interno del container.
COPY ["TelegramBotClassifica/", "TelegramBotClassifica/"]
WORKDIR "/src/TelegramBotClassifica"
RUN dotnet publish "TelegramBotClassifica.csproj" -c Release -o /app/publish

# Fase 2: Creazione dell'immagine finale
# Usiamo l'immagine runtime di ASP.NET, molto più leggera dell'SDK, per l'esecuzione.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copia solo l'applicazione pubblicata dalla fase di build.
COPY --from=build /app/publish .

# Comando che verrà eseguito all'avvio del container.
ENTRYPOINT ["dotnet", "TelegramBotClassifica.dll"]
