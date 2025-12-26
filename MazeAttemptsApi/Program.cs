using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// строка подключения из appsettings.json (ConnectionStrings:Postgres)
var connString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=maze_db;";

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

// -------------------- HEALTH --------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// -------------------- CREATE ATTEMPT --------------------
// Теперь сохраняем: seed + width/height + create_finish_area + use_right_hand_rule
app.MapPost("/attempts", async (CreateAttemptDto dto) =>
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = @"
        INSERT INTO attempts (maze_seed, maze_width, maze_height, create_finish_area, use_right_hand_rule)
        VALUES (@seed, @w, @h, @finish, @right)
        RETURNING id;
    ";

    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("seed", dto.maze_seed);
    cmd.Parameters.AddWithValue("w", dto.maze_width);
    cmd.Parameters.AddWithValue("h", dto.maze_height);
    cmd.Parameters.AddWithValue("finish", dto.create_finish_area);
    cmd.Parameters.AddWithValue("right", dto.use_right_hand_rule);

    var idObj = await cmd.ExecuteScalarAsync();
    int attemptId = Convert.ToInt32(idObj);

    return Results.Ok(new AttemptCreatedDto(attemptId));
});

// -------------------- INSERT ACTIONS (BATCH) --------------------
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

// -------------------- LATEST ATTEMPTS (for menu) --------------------
// GET /attempts/latest?limit=10
// Возвращаем +create_finish_area +use_right_hand_rule
app.MapGet("/attempts/latest", async (int? limit) =>
{
    int take = Math.Clamp(limit ?? 10, 1, 50);

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = @"
        SELECT
            a.id as attempt_id,
            a.maze_seed,
            a.maze_width,
            a.maze_height,
            a.create_finish_area,
            a.use_right_hand_rule,
            a.created_at,
            COALESCE(MAX(c.time_sec), 0) as duration_sec
        FROM attempts a
        LEFT JOIN car_actions c ON c.attempt_id = a.id
        GROUP BY a.id
        ORDER BY a.id DESC
        LIMIT @take;
    ";

    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("take", take);

    var list = new List<AttemptListItemDto>();
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        list.Add(new AttemptListItemDto(
            attempt_id: reader.GetInt32(0),
            maze_seed: reader.GetInt32(1),
            maze_width: reader.GetInt32(2),
            maze_height: reader.GetInt32(3),
            create_finish_area: reader.GetBoolean(4),
            use_right_hand_rule: reader.GetBoolean(5),
            created_at: reader.GetDateTime(6),
            duration_sec: reader.GetFloat(7)
        ));
    }

    return Results.Ok(new AttemptsListWrapper(list.ToArray()));
});

// -------------------- GET ACTIONS BY ATTEMPT (for replay) --------------------
app.MapGet("/attempts/{attemptId:int}/actions", async (int attemptId) =>
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = @"
        SELECT time_sec, action, pos_x, pos_y
        FROM car_actions
        WHERE attempt_id = @id
        ORDER BY time_sec ASC;
    ";

    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", attemptId);

    var records = new List<ActionDto>();
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        records.Add(new ActionDto(
            time_sec: reader.GetFloat(0),
            action: reader.GetString(1),
            pos_x: reader.IsDBNull(2) ? null : reader.GetInt32(2),
            pos_y: reader.IsDBNull(3) ? null : reader.GetInt32(3)
        ));
    }

    return Results.Ok(new ActionsWrapperDto(records.ToArray()));
});

app.Run("http://0.0.0.0:5081");
