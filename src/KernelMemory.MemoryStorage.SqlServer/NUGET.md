# SQL Server memory for Kernel Memory

This is a connector for the Kernel Memory.

It provides a connection to a SQL database for the Kernel Memory.

## About Kernel Memory

**Kernel Memory** (KM) is a multi-modal **AI Service** specialized in the efficient indexing of datasets through custom continuous data hybrid pipelines, with support for **Retrieval Augmented Generation (RAG)**, synthetic memory, prompt engineering, and custom semantic memory processing.

## Installation

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