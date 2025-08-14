# Usa un'immagine SDK di .NET 8.0 per la fase di build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia il file del progetto e ripristina le dipendenze
# CORREZIONE QUI: Rimuoviamo il prefisso della cartella
COPY ["TelegramBotClassifica.csproj", "./"]
RUN dotnet restore "TelegramBotClassifica.csproj"

# Copia tutti i file sorgente e pubblica l'applicazione
COPY . .
WORKDIR "/src"
RUN dotnet publish "TelegramBotClassifica.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Usa un'immagine runtime di ASP.NET Core 8.0 più leggera per l'esecuzione finale
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copia i file pubblicati dall'immagine di build
COPY --from=build /app/publish .

# Espone la porta su cui il bot è in ascolto
EXPOSE 8080

# Comando di avvio dell'applicazione
ENTRYPOINT ["dotnet", "TelegramBotClassifica.dll"]