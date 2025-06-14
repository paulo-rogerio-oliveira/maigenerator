# Model Creation Pattern (MCP) Tool for Cursor

This is a Model Creation Pattern (MCP) tool that generates C# model classes from database tables, integrated with Cursor IDE.

## Setup

1. Make sure the API is running on `http://localhost:5000`
2. Place the `cursor-mcp.json` file in your project root
3. Restart Cursor to load the MCP tool

## Using in Cursor

1. Open the command palette in Cursor (Ctrl+Shift+P or Cmd+Shift+P)
2. Type `mcp` to see available commands
3. Select `mcp model-creator` and provide the table name
4. The generated model will be inserted at your cursor position

Example usage:
```
mcp model-creator pessoa
mcp model-creator abastecimento
```

## API Endpoints

### List Available Tools
```http
GET /mcp/tools
```

### Create Model
```http
POST /mcp/model/create
Content-Type: application/json

{
    "tableName": "your_table_name"
}
```

## Response Format

```json
{
    "success": true,
    "data": {
        "content": "Generated model content...",
        "tableName": "your_table_name",
        "generatedAt": "2024-03-21T10:30:00Z"
    }
}
```

## Error Handling

If an error occurs, the response will be:
```json
{
    "success": false,
    "error": "Error message"
}
```

## Configuration

The tool uses the connection string from `App_Data/config.json`. Make sure this file exists and contains a valid connection string.

## Cursor Integration

The tool is integrated with Cursor through the `cursor-mcp.json` configuration file, which provides:
- Command definition
- Usage instructions
- Example commands
- Request/response schemas
- Error handling 