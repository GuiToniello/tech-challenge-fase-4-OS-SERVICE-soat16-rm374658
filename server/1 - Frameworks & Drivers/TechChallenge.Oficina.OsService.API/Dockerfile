# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primeiro os csproj para maximizar o cache do restore.
# Se só o código mudar, essa etapa tende a ser reaproveitada.
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.API/TechChallenge.Oficina.OsService.API.csproj", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.API/"]
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.DB/TechChallenge.Oficina.OsService.DB.csproj", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.DB/"]
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.Messaging/TechChallenge.Oficina.OsService.Messaging.csproj", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.Messaging/"]
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.NoSql/TechChallenge.Oficina.OsService.NoSql.csproj", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.NoSql/"]
COPY ["2 - Interface Adapters/TechChallenge.Oficina.OsService.Adapters/TechChallenge.Oficina.OsService.Adapters.csproj", "2 - Interface Adapters/TechChallenge.Oficina.OsService.Adapters/"]
COPY ["2 - Interface Adapters/TechChallenge.Oficina.OsService.Controllers/TechChallenge.Oficina.OsService.Controllers.csproj", "2 - Interface Adapters/TechChallenge.Oficina.OsService.Controllers/"]
COPY ["3 - Application Business Rules/TechChallenge.Oficina.OsService.UseCases/TechChallenge.Oficina.OsService.UseCases.csproj", "3 - Application Business Rules/TechChallenge.Oficina.OsService.UseCases/"]
COPY ["4 - Enterprise Business Rules/TechChallenge.Oficina.OsService.Entities/TechChallenge.Oficina.OsService.Entities.csproj", "4 - Enterprise Business Rules/TechChallenge.Oficina.OsService.Entities/"]

RUN dotnet restore "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.API/TechChallenge.Oficina.OsService.API.csproj"

# Depois do restore, copia apenas os projetos necessários para compilar a solução.
# Isso evita trazer arquivos extras do contexto de build para a imagem.
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.API/", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.API/"]
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.DB/", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.DB/"]
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.Messaging/", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.Messaging/"]
COPY ["1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.NoSql/", "1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.NoSql/"]
COPY ["2 - Interface Adapters/TechChallenge.Oficina.OsService.Adapters/", "2 - Interface Adapters/TechChallenge.Oficina.OsService.Adapters/"]
COPY ["2 - Interface Adapters/TechChallenge.Oficina.OsService.Controllers/", "2 - Interface Adapters/TechChallenge.Oficina.OsService.Controllers/"]
COPY ["3 - Application Business Rules/TechChallenge.Oficina.OsService.UseCases/", "3 - Application Business Rules/TechChallenge.Oficina.OsService.UseCases/"]
COPY ["4 - Enterprise Business Rules/TechChallenge.Oficina.OsService.Entities/", "4 - Enterprise Business Rules/TechChallenge.Oficina.OsService.Entities/"]
WORKDIR "/src/1 - Frameworks & Drivers/TechChallenge.Oficina.OsService.API"
RUN dotnet publish "TechChallenge.Oficina.OsService.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Instala uma ferramenta leve de HTTP para o HEALTHCHECK do container.
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*

# Usa apenas o runtime para manter a imagem final menor e mais segura.
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

# A imagem oficial já expõe um usuário não-root via APP_UID.
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
	CMD curl --fail --silent --show-error http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TechChallenge.Oficina.OsService.API.dll"]
