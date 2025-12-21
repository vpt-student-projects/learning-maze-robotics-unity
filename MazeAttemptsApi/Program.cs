using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Поменяй YOUR_PASS и YOUR_DB
var connString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Username=postgres;Password=YOUR_PASS;Database=YOUR_DB;";

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

// Проверка что сервер жив
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Создать попытку
app.MapPost("/attempts", async (CreateAttemptDto dto) =>
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = @"
        INSERT INTO attempts (maze_seed, maze_width, maze_height)
        VALUES (@seed, @w, @h)
        RETURNING id;
    ";

    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("seed", dto.maze_seed);
    cmd.Parameters.AddWithValue("w", dto.maze_width);
    cmd.Parameters.AddWithValue("h", dto.maze_height);

    var idObj = await cmd.ExecuteScalarAsync();
    int attemptId = Convert.ToInt32(idObj);

    return Results.Ok(new AttemptCreatedDto(attemptId));
});

// Добавить действия (пачкой)
app.MapPost("/attempts/{attemptId:int}/actions", async (int attemptId, ActionsWrapperDto wrapper) =>
{
    if (wrapper?.records == null || wrapper.records.Length == 0)
        return Results.BadRequest(new { error = "records is empty" });

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var tx = await conn.BeginTransactionAsync();

    const string sql = @"
        INSERT INTO car_actions (attempt_id, time_sec, action, pos_x, pos_y)
        VALUES (@attempt_id, @time_sec, @action, @pos_x, @pos_y);
    ";

    int inserted = 0;

    await using (var cmd = new NpgsqlCommand(sql, conn, tx))
    {
        var pAttempt = cmd.Parameters.Add("attempt_id", NpgsqlTypes.NpgsqlDbType.Integer);
        var pTime = cmd.Parameters.Add("time_sec", NpgsqlTypes.NpgsqlDbType.Real);
        var pAction = cmd.Parameters.Add("action", NpgsqlTypes.NpgsqlDbType.Varchar);
        var pX = cmd.Parameters.Add("pos_x", NpgsqlTypes.NpgsqlDbType.Integer);
        var pY = cmd.Parameters.Add("pos_y", NpgsqlTypes.NpgsqlDbType.Integer);

        foreach (var r in wrapper.records)
        {
            pAttempt.Value = attemptId;
            pTime.Value = r.time_sec;
            pAction.Value = r.action ?? "";

            pX.Value = (object?)r.pos_x ?? DBNull.Value;
            pY.Value = (object?)r.pos_y ?? DBNull.Value;

            inserted += await cmd.ExecuteNonQueryAsync();
        }
    }

    await tx.CommitAsync();
    return Results.Ok(new { inserted });
});

// фиксируем порт
app.Run("http://0.0.0.0:5081");
