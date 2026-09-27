# ChatApp — publicação

## Variáveis obrigatórias

Defina a connection string por variável de ambiente:

`ConnectionStrings__DefaultConnection`

Também é recomendado definir:

`ASPNETCORE_ENVIRONMENT=Production`

## Docker

```bash
docker build -t chatapp .
docker run -d --name chatapp -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e 'ConnectionStrings__DefaultConnection=<SQL_SERVER_CONNECTION_STRING>' \
  chatapp
```

O ASP.NET Core deve ficar atrás de HTTPS/reverse proxy em produção. O SignalR usa `/hubs/chat`, `/hubs/call` e `/hubs/group`.

## SQL Server

A aplicação aplica as migrations existentes no arranque e cria as tabelas adicionais de grupos/status pelo inicializador de schema. Em produção, faça backup antes da primeira publicação.

## WebRTC

Para chamadas fora da rede local, configure HTTPS e considere adicionar TURN. STUN sozinho não garante conectividade em todas as redes/NAT.

## Uploads

O armazenamento local em `wwwroot/uploads` é adequado para testes. Em produção com múltiplas instâncias, use armazenamento persistente/objeto.
