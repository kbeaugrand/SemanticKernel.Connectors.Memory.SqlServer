# Semantic Kernel & Kernel Memory - SQL Connector

[![Build & Test](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/build_test.yml/badge.svg)](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/build_test.yml)
[![Create Release](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/publish.yml/badge.svg)](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer/actions/workflows/publish.yml)
[![Version](https://img.shields.io/github/v/release/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer)](https://img.shields.io/github/v/release/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer)
[![License](https://img.shields.io/github/license/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer)](https://img.shields.io/github/v/release/kbeaugrand/SemanticKernel.Connectors.Memory.SqlServer)

This is a connector for the [Semantic Kernel](https://aka.ms/semantic-kernel).

It provides a connection to a SQL database for the Semantic Kernel for the memories.

## About Semantic Kernel

**Semantic Kernel (SK)** is a lightweight SDK enabling integration of AI Large
Language Models (LLMs) with conventional programming languages. The SK
extensible programming model combines natural language **semantic functions**,
traditional code **native functions**, and **embeddings-based memory** unlocking
new potential and adding value to applications with AI.

Please take a look at [Semantic Kernel](https://aka.ms/semantic-kernel) for more information.

## About Kernel Memory

**Kernel Memory** (KM) is a multi-modal **AI Service** specialized in the efficient indexing of datasets through custom continuous data hybrid pipelines, with support for **Retrieval Augmented Generation (RAG)**, synthetic memory, prompt engineering, and custom semantic memory processing.


## Semantic Kernel Plugin

To install this memory store, you need to add the required nuget package to your project:

```dotnetcli
dotnet add package SemanticKernel.Connectors.Memory.SqlServer --version 1.0.0-beta1
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

## Kernel Memory Plugin

```bash
dotnet add package KernelMemory.MemoryStorage.SqlServer
```

## Usage

To add your SQL Server memory connector, add the following statements to your kernel memory initialization code:

```csharp
using SemanticKernel.Connectors.Memory.SqlServer;
...
var memory = new KernelMemoryBuilder()
    .WithOpenAIDefaults(Env.Var("OPENAI_API_KEY"))
    .WithSqlServerMemoryDb("YouSqlConnectionString")
            ...
    .Build<MemoryServerless>();
```

The memory store will populate all the needed tables during startup and let you focus on the development of your plugin.

## License

This project is licensed under the [MIT License](LICENSE).
