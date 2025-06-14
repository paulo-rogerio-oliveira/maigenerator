# MAI Generator

A .NET-based tool for generating C# model classes from database tables using the Model Creation Pattern (MCP).

## Features

- Generate C# model classes from database tables
- Support for SQL Server databases
- Customizable model templates
- MCP (Model Creation Pattern) integration
- RESTful API endpoints for model generation

## Prerequisites

- .NET 8.0 SDK
- SQL Server database
- Visual Studio 2022 or Visual Studio Code

## Configuration

1. Create an `App_Data` directory in the project root
2. Add a `config.json` file with your database connection string:
```json
{
    "connectionString": "your_connection_string_here"
}
```

## Getting Started

1. Clone the repository
2. Navigate to the project directory
3. Run the following commands:
```bash
dotnet restore
dotnet build
dotnet run --project mygen-api
```

## API Endpoints

### Create Model
```http
POST /mcp/model/create
Content-Type: application/json

{
    "tableName": "your_table_name"
}
```

### List Available Tools
```http
GET /mcp/tools
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details. 