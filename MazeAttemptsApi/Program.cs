using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=maze_db;";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok"
    });
});

// ------------------------------------------------------------
// AUTH: REGISTER
// POST /auth/register
// ------------------------------------------------------------
app.MapPost("/auth/register", async (RegisterDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.username) ||
        string.IsNullOrWhiteSpace(dto.email) ||
        string.IsNullOrWhiteSpace(dto.password))
    {
        return Results.BadRequest(new AuthResponseDto(
            success: false,
            message: "Введите логин, email и пароль",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    if (dto.password.Length < 4)
    {
        return Results.BadRequest(new AuthResponseDto(
            success: false,
            message: "Пароль должен быть не короче 4 символов",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var checkCmd = new NpgsqlCommand(
        """
        SELECT COUNT(*)
        FROM users
        WHERE username = @username OR email = @email;
        """,
        conn
    );

    checkCmd.Parameters.AddWithValue("username", dto.username);
    checkCmd.Parameters.AddWithValue("email", dto.email);

    long existingUsersCount = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);

    if (existingUsersCount > 0)
    {
        return Results.Conflict(new AuthResponseDto(
            success: false,
            message: "Пользователь с таким логином или email уже существует",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    string passwordHash = PasswordHasher.HashPassword(dto.password);

    await using var insertCmd = new NpgsqlCommand(
        """
        INSERT INTO users (
            role_id,
            username,
            email,
            password_hash,
            full_name,
            created_at,
            updated_at,
            is_active
        )
        VALUES (
            (SELECT id FROM roles WHERE name = 'user'),
            @username,
            @email,
            @password_hash,
            @full_name,
            CURRENT_TIMESTAMP,
            CURRENT_TIMESTAMP,
            TRUE
        )
        RETURNING id;
        """,
        conn
    );

    insertCmd.Parameters.AddWithValue("username", dto.username);
    insertCmd.Parameters.AddWithValue("email", dto.email);
    insertCmd.Parameters.AddWithValue("password_hash", passwordHash);
    insertCmd.Parameters.AddWithValue("full_name", (object?)dto.full_name ?? DBNull.Value);

    int userId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());

    return Results.Ok(new AuthResponseDto(
        success: true,
        message: "Регистрация выполнена",
        user_id: userId,
        username: dto.username,
        email: dto.email,
        full_name: dto.full_name
    ));
});

// ------------------------------------------------------------
// AUTH: LOGIN
// POST /auth/login
// ------------------------------------------------------------
app.MapPost("/auth/login", async (LoginDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.login) ||
        string.IsNullOrWhiteSpace(dto.password))
    {
        return Results.BadRequest(new AuthResponseDto(
            success: false,
            message: "Введите логин/email и пароль",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(
        """
        SELECT id, username, email, password_hash, full_name, is_active
        FROM users
        WHERE username = @login OR email = @login
        LIMIT 1;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("login", dto.login);

    await using var reader = await cmd.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
    {
        return Results.NotFound(new AuthResponseDto(
            success: false,
            message: "Пользователь не найден",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    int userId = reader.GetInt32(0);
    string username = reader.GetString(1);
    string email = reader.GetString(2);
    string passwordHash = reader.GetString(3);
    string? fullName = reader.IsDBNull(4) ? null : reader.GetString(4);
    bool isActive = reader.GetBoolean(5);

    if (!isActive)
    {
        return Results.BadRequest(new AuthResponseDto(
            success: false,
            message: "Пользователь заблокирован",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    bool passwordIsCorrect = PasswordHasher.VerifyPassword(dto.password, passwordHash);

    if (!passwordIsCorrect)
    {
        return Results.BadRequest(new AuthResponseDto(
            success: false,
            message: "Неверный пароль",
            user_id: null,
            username: null,
            email: null,
            full_name: null
        ));
    }

    return Results.Ok(new AuthResponseDto(
        success: true,
        message: "Вход выполнен",
        user_id: userId,
        username: username,
        email: email,
        full_name: fullName
    ));
});

// ------------------------------------------------------------
// ATTEMPTS: CREATE ATTEMPT
// POST /attempts
// ------------------------------------------------------------
app.MapPost("/attempts", async (CreateAttemptDto dto) =>
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = """
        INSERT INTO attempts (
            maze_seed,
            maze_width,
            maze_height,
            create_finish_area,
            use_right_hand_rule,
            user_id
        )
        VALUES (
            @seed,
            @width,
            @height,
            @create_finish_area,
            @create_finish_area_in_corner,
            @user_id
        )
        RETURNING id;
        """;

    await using var cmd = new NpgsqlCommand(sql, conn);

    cmd.Parameters.AddWithValue("seed", dto.maze_seed);
    cmd.Parameters.AddWithValue("width", dto.maze_width);
    cmd.Parameters.AddWithValue("height", dto.maze_height);
    cmd.Parameters.AddWithValue("create_finish_area", dto.create_finish_area);

    // Важно:
    // Unity отправляет create_finish_area_in_corner.
    // В БД пока колонка называется use_right_hand_rule.
    // Поэтому здесь просто маппим старое Unity-поле в существующую колонку БД.
    cmd.Parameters.AddWithValue("create_finish_area_in_corner", dto.create_finish_area_in_corner);

    cmd.Parameters.AddWithValue("user_id", (object?)dto.user_id ?? DBNull.Value);

    var idObj = await cmd.ExecuteScalarAsync();
    int attemptId = Convert.ToInt32(idObj);

    return Results.Ok(new AttemptCreatedDto(attemptId));
});

// ------------------------------------------------------------
// ATTEMPTS: SAVE ACTIONS
// POST /attempts/{attemptId}/actions
// ------------------------------------------------------------
app.MapPost("/attempts/{attemptId:int}/actions", async (int attemptId, ActionsWrapperDto wrapper) =>
{
    if (wrapper?.records == null || wrapper.records.Length == 0)
    {
        return Results.BadRequest(new
        {
            error = "records is empty"
        });
    }

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var tx = await conn.BeginTransactionAsync();

    const string sql = """
        INSERT INTO car_actions (
            attempt_id,
            time_sec,
            action,
            pos_x,
            pos_y
        )
        VALUES (
            @attempt_id,
            @time_sec,
            @action,
            @pos_x,
            @pos_y
        );
        """;

    int inserted = 0;

    await using (var cmd = new NpgsqlCommand(sql, conn, tx))
    {
        var pAttemptId = cmd.Parameters.Add("attempt_id", NpgsqlTypes.NpgsqlDbType.Integer);
        var pTimeSec = cmd.Parameters.Add("time_sec", NpgsqlTypes.NpgsqlDbType.Real);
        var pAction = cmd.Parameters.Add("action", NpgsqlTypes.NpgsqlDbType.Varchar);
        var pPosX = cmd.Parameters.Add("pos_x", NpgsqlTypes.NpgsqlDbType.Integer);
        var pPosY = cmd.Parameters.Add("pos_y", NpgsqlTypes.NpgsqlDbType.Integer);

        foreach (var record in wrapper.records)
        {
            pAttemptId.Value = attemptId;
            pTimeSec.Value = record.time_sec;
            pAction.Value = record.action ?? "";

            pPosX.Value = (object?)record.pos_x ?? DBNull.Value;
            pPosY.Value = (object?)record.pos_y ?? DBNull.Value;

            inserted += await cmd.ExecuteNonQueryAsync();
        }
    }

    await tx.CommitAsync();

    return Results.Ok(new
    {
        inserted
    });
});

// ------------------------------------------------------------
// ATTEMPTS: LATEST LIST
// GET /attempts/latest?limit=10
// ------------------------------------------------------------
app.MapGet("/attempts/latest", async (int? limit) =>
{
    int take = Math.Clamp(limit ?? 10, 1, 50);

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = """
        SELECT
            a.id AS attempt_id,
            a.maze_seed,
            a.maze_width,
            a.maze_height,
            a.create_finish_area,
            a.use_right_hand_rule,
            a.created_at,
            COALESCE(MAX(c.time_sec), 0) AS duration_sec,
            a.user_id,
            u.username
        FROM attempts a
        LEFT JOIN car_actions c ON c.attempt_id = a.id
        LEFT JOIN users u ON u.id = a.user_id
        GROUP BY a.id, u.username
        ORDER BY a.id DESC
        LIMIT @take;
        """;

    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("take", take);

    var items = new List<AttemptListItemDto>();

    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        items.Add(new AttemptListItemDto(
            attempt_id: reader.GetInt32(0),
            maze_seed: reader.GetInt32(1),
            maze_width: reader.GetInt32(2),
            maze_height: reader.GetInt32(3),
            create_finish_area: reader.GetBoolean(4),

            // Важно:
            // Из БД читаем use_right_hand_rule,
            // но в Unity возвращаем как create_finish_area_in_corner.
            create_finish_area_in_corner: reader.GetBoolean(5),

            created_at: reader.GetDateTime(6),
            duration_sec: reader.GetFloat(7),
            user_id: reader.IsDBNull(8) ? null : reader.GetInt32(8),
            username: reader.IsDBNull(9) ? null : reader.GetString(9)
        ));
    }

    return Results.Ok(new AttemptsListWrapper(items.ToArray()));
});

// ------------------------------------------------------------
// ATTEMPTS: GET ACTIONS BY ATTEMPT
// GET /attempts/{attemptId}/actions
// ------------------------------------------------------------
app.MapGet("/attempts/{attemptId:int}/actions", async (int attemptId) =>
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    const string sql = """
        SELECT
            time_sec,
            action,
            pos_x,
            pos_y
        FROM car_actions
        WHERE attempt_id = @attempt_id
        ORDER BY time_sec ASC;
        """;

    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("attempt_id", attemptId);

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

// ------------------------------------------------------------
// PASSWORD HASHER
// ------------------------------------------------------------
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;

    public static string HashPassword(string password)
    {
        byte[] salt = new byte[SaltSize];

        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        using Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256
        );

        byte[] hash = pbkdf2.GetBytes(HashSize);

        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        string[] parts = storedHash.Split('$');

        if (parts.Length != 4 || parts[0] != "PBKDF2")
        {
            return false;
        }

        int iterations = int.Parse(parts[1]);
        byte[] salt = Convert.FromBase64String(parts[2]);
        byte[] expectedHash = Convert.FromBase64String(parts[3]);

        using Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256
        );

        byte[] actualHash = pbkdf2.GetBytes(expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}