# AI-Agent-SemanticKernel-Chat
This is a console application that demonstrates the integration of Microsoft's Semantic Kernel with Ollama to create a flexible chat interface supporting multiple local LLM models. 

## Features

- **"Production-Ready Architecture"**: Proper dependency injection, logging, and error handling
- **Chat History Persistence**: PostgreSQL database with JSON storage for chat sessions
- **Streaming Responses**: Real-time response streaming for better user experience
- **Session Management**: Create, list, switch between, and delete chat sessions
- **Comprehensive Logging**: Structured logging with Serilog to console and files
- **Configuration Management**: Environment-specific settings with appsettings.json
- **Error Handling**: Graceful error handling throughout the application
- **Cancellation Support**: Proper async cancellation token handling
- **Console Interface**: Rich console interface with command support

## Prerequisites

1. **.NET 8.0 SDK** or later
2. **PostgreSQL** database server
3. **Ollama** running locally with your preferred model

## Quick Start

### 1. Install Dependencies

```bash
# Install .NET packages (automatically handled by the project file)
dotnet restore
```

### 2. Set up PostgreSQL Database

#### Option A: Automatic Setup (Recommended)

The application will automatically create the database tables on first run. Simply ensure PostgreSQL is running and the connection string is correct.

#### Option B: Manual Setup

Use the included `database-setup.sql` script for manual setup:

```sql
-- database-setup.sql
-- Run this script to set up your PostgreSQL database

-- Create database (run this as a superuser)
-- CREATE DATABASE ai_agent_chat;
-- CREATE DATABASE ai_agent_chat_dev;

-- Connect to your database and create the tables
-- The application will auto-create tables, but you can run this manually if needed

CREATE TABLE IF NOT EXISTS chat_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id VARCHAR(255) NOT NULL,
    title VARCHAR(500) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_message_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    messages JSONB DEFAULT '[]'::jsonb
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_chat_sessions_user_id_last_message 
ON chat_sessions(user_id, last_message_at DESC);

-- Optional: Create a user for the application
-- CREATE USER ai_agent_user WITH PASSWORD 'your_password';
-- GRANT ALL PRIVILEGES ON DATABASE ai_agent_chat TO ai_agent_user;
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO ai_agent_user;
-- GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO ai_agent_user;
```

### 3. Configure Application Settings

Update `appsettings.json` with your database connection string:

```json
{
  "ConnectionString": "Host=localhost;Database=ai_agent_chat;Username=ai_agent_user;Password=your_secure_password_here",
  "OllamaApiUrl": "http://localhost:11434",
  "DefaultModel": "gemma2:2b"
}
```

### 4. Start Ollama

Make sure Ollama is running and your model is available:

```bash
# Start Ollama
ollama serve

# Pull your model (if not already available)
ollama pull gemma2:2b
```

### 5. Run the Application

```bash
dotnet run
```

## Configuration Options

### appsettings.json

```json
{
  "ConnectionString": "Host=localhost;Database=ai_agent_chat;Username=username;Password=password",
  "OllamaApiUrl": "http://localhost:11434",
  "DefaultModel": "gemma2:2b",
  "MaxHistoryMessages": 100,
  "CommandTimeoutSeconds": 30,
  "EnableStreamingResponse": true
}
```

### Configuration Properties

- **ConnectionString**: PostgreSQL connection string
- **OllamaApiUrl**: URL to your Ollama API server
- **DefaultModel**: The Ollama model to use for chat completions
- **MaxHistoryMessages**: Maximum number of messages to keep in context
- **CommandTimeoutSeconds**: Database command timeout
- **EnableStreamingResponse**: Whether to use streaming responses

## Console Commands

The application provides a rich console interface with the following commands:

- `/new <title>` - Start a new chat session
- `/list` - List all your chat sessions
- `/switch <session-id>` - Switch to a different session
- `/delete <session-id>` - Delete a chat session
- `/help` - Show help message
- `/exit` - Exit the application

## Architecture

### Project Structure

```
AI_Agent_SemanticKernel_Chat/
├── Models/
│   ├── AppSettings.cs          # Configuration model
│   └── ChatSession.cs          # Chat session and message models
├── Repositories/
│   ├── IChatHistoryRepository.cs
│   └── PostgreSqlChatHistoryRepository.cs
├── Services/
│   ├── IChatService.cs
│   ├── ChatService.cs          # Business logic for chat operations
│   ├── ChatConsoleService.cs   # Console interface service
│   └── OllamaChatCompletionService.cs # Ollama integration
└── Program.cs                  # Application entry point
```

### Key Components

1. **ChatService**: High-level business logic for chat operations
2. **PostgreSqlChatHistoryRepository**: Database operations for chat persistence
3. **OllamaChatCompletionService**: Enhanced Ollama integration with error handling
4. **ChatConsoleService**: Console interface with session management

## Database Schema

The application uses an optimized PostgreSQL schema:

### Tables

**chat_sessions**
```sql
CREATE TABLE IF NOT EXISTS chat_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id VARCHAR(255) NOT NULL,
    title VARCHAR(500) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_message_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    messages JSONB DEFAULT '[]'::jsonb
);
```

### Indexes

```sql
-- Performance index for user session queries
CREATE INDEX IF NOT EXISTS idx_chat_sessions_user_id_last_message 
ON chat_sessions(user_id, last_message_at DESC);
```

### Message Storage

Chat messages are stored as JSONB with the following structure:

```json
[
  {
    "id": "uuid",
    "role": "user|assistant|system",
    "content": "message content",
    "timestamp": "2024-01-01T00:00:00.000Z",
    "metadata": {}
  }
]
```

### Logging

The application uses Serilog for structured logging:

- **Console**: Real-time logging output
- **File**: Daily rolling log files in `logs/` directory
- **Structured**: JSON-formatted logs with contextual information

Log levels can be configured per environment in appsettings files.

## Error Handling

The application includes comprehensive error handling:

- **Database Errors**: Automatic retry logic and detailed error logging
- **Network Errors**: Proper handling of Ollama connectivity issues
- **Validation Errors**: Input validation and user-friendly error messages
- **Cancellation**: Proper support for operation cancellation

## Performance Considerations

- **Connection Pooling**: Uses Npgsql connection pooling
- **Streaming**: Implements streaming responses for better perceived performance
- **History Limiting**: Limits chat history to prevent context overflow
- **Async/Await**: Full async implementation for better scalability
- **Database Indexes**: Optimized indexes for common query patterns
- **JSONB Storage**: Efficient JSON storage and querying capabilities

## Security Considerations

- **SQL Injection**: Uses parameterized queries
- **Connection Security**: Supports SSL connections to PostgreSQL
- **Input Sanitization**: Proper input validation and sanitization
- **Configuration**: Sensitive settings can be externalized
- **User Permissions**: Database user with minimal required privileges

## Troubleshooting

### Common Issues

1. **Database Connection Failed**
   - Verify PostgreSQL is running
   - Check connection string in appsettings.json
   - Ensure database exists and user has permissions
   - Run the database-setup.sql script if tables don't exist

2. **Ollama Connection Failed**
   - Ensure Ollama is running: `ollama serve`
   - Verify the model is available: `ollama list`
   - Check the OllamaApiUrl in configuration

3. **Model Not Found**
   - Pull the model: `ollama pull gemma2:2b`
   - Update DefaultModel in configuration

4. **Permission Denied on Database**
   - Ensure the database user has proper permissions
   - Run the GRANT commands from the database-setup.sql script

### Database Health Check

```sql
-- Check if tables exist
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public';

-- Check table structure
\d chat_sessions

-- Check indexes
SELECT indexname, indexdef FROM pg_indexes 
WHERE tablename = 'chat_sessions';
```

### Logs

Check the logs in the `logs/` directory for detailed error information.

## Contributing

This is a template that can be extended with:

- Web API interface
- Authentication and authorization
- Multi-user support
- Advanced chat features
- Model switching capabilities
- Chat export/import functionality