FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore backend/OneCart.Api/OneCart.Api.csproj
RUN dotnet publish backend/OneCart.Api/OneCart.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /var/onecart/product-images /var/onecart/data-protection-keys /var/onecart/installation \
    && chown -R $APP_UID:$APP_UID /var/onecart
USER $APP_UID
ENTRYPOINT ["dotnet", "OneCart.Api.dll"]
