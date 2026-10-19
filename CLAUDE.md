# Tasks API

Minimal ASP.NET (.NET 9) API over MySQL 8.4 using Dapper and MySqlConnector. A practice project.

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project src/TasksApi.Api`
- Start DB: `docker start tasks-mysql`
- DB shell: `docker exec -it tasks-mysql mysql -uroot -pdevpass tasksdb`

## Schema (managed by hand, do not alter without asking)
- `users`: id, email (unique), name, created_at
- `tasks`: id, user_id (FK to users, ON DELETE CASCADE), title, status ENUM('todo','in_progress','done'), due_date, created_at

## Conventions
- Minimal APIs in `Program.cs` or small endpoint files. No controllers, no Entity Framework.
- Dapper with parameterized SQL only. Never build SQL with string concatenation.
- Connection string comes from config key `ConnectionStrings:Default` (local dev: localhost:3306, tasksdb, root/devpass).
- Every endpoint needs an xUnit test. Run `dotnet test` before reporting a task as done.
- Work on a feature branch with small commits. Never commit directly to main.
