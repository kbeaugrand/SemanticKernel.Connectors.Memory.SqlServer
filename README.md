# Semantic Kernel - SQL Connector

[![Build & Test](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/build_test.yml/badge.svg)](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/build_test.yml)

[![Create Release](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/publish.yml/badge.svg)](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/publish.yml)

This is a connector for the [Semantic Kernel](https://aka.ms/semantic-kernel).

It provides a connection to a SQL database for the Semantic Kernel for the memories.

## About Semantic Kernel

**Semantic Kernel (SK)** is a lightweight SDK enabling integration of AI Large
Language Models (LLMs) with conventional programming languages. The SK
extensible programming model combines natural language **semantic functions**,
traditional code **native functions**, and **embeddings-based memory** unlocking
new potential and adding value to applications with AI.

Please take a look at [Semantic Kernel](https://aka.ms/semantic-kernel) for more information.

## Installation

To install this memory store, you need to add the required nuget package to your project:

```dotnetcli
dotnet nuget add source https://nuget.pkg.github.com/kbeaugrand/index.json --name github
dotnet add package Microsoft.SemanticKernel.Connectors.Memory.SqlServer --version 0.0.1-alpha
```

## Usage

To add your SQL Server memory connector, add the following statements to your kernel initialization code:

```csharp
using SemanticKernel.Connectors.Memory.SqlServer;
...
var kernel = Kernel.Builder
            ...
                .WithMemoryStorage(await SqlServerMemoryStore.ConnectAsync(connectionString: "Server=.;Database=SK;Trusted_Connection=True;"))
            ...
                .Build();
```

The memory store will populate all the needed tables during startup and let you focus on the development of your plugin.

## License

This project is licensed under the [MIT License](LICENSE).
