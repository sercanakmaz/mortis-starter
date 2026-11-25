# Acme

A .NET 10 backend built from domain-driven-design building blocks: aggregates, child entities, value objects, domain events, repositories, domain services and commands, on EF Core and PostgreSQL.

## Run it

```bash
docker compose up -d postgres
dotnet build
dotnet test
dotnet ef database update --project src/Acme.Infrastructure
dotnet run --project src/Acme.Api
```

`dotnet run --project src/Acme.Console --` lists the scenarios and the commands you can run by name, for example `-- CreateProduct '{"sku":"BOLT","name":"Bolt","stock":10}'`. In VS Code, F5 on "Console: run a command or scenario" asks for the name and the JSON.

## Read next

- [AGENTS.md](AGENTS.md): the rules, and every place an aggregate touches.
- [docs/architecture.md](docs/architecture.md): what happens when a command runs, how events flow, how persistence behaves.
- [MORTIS.md](MORTIS.md): the decisions this project was started with.
- [docs/adr/](docs/adr/): why it is built this way (the dispatcher, commands as interfaces, the user on the command, event handlers, the Console, handlers as use cases, one context on the command).
