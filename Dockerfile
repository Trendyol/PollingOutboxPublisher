#Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /workdir

COPY ./src/PollingOutboxPublisher.csproj ./src/PollingOutboxPublisher.csproj

RUN dotnet restore ./src/PollingOutboxPublisher.csproj

COPY . .

# Remove config files to prevent copying them to the final image.
# If you want to copy them to the final image, remove the following lines.
RUN rm -f ./src/appsettings*
RUN rm -f ./src/config/config.json
RUN rm -f ./src/config/secret.json

RUN dotnet publish ./src/PollingOutboxPublisher.csproj -c Release -o /publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/runtime:8.0

COPY --from=build-env /publish /publish

WORKDIR /publish

ENTRYPOINT [ "dotnet", "PollingOutboxPublisher.dll" ]
