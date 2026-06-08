using System;

public record CreateAttemptDto(
    int maze_seed,
    int maze_width,
    int maze_height,
    bool create_finish_area,
    bool create_finish_area_in_corner,
    int? user_id
);

public record AttemptCreatedDto(int attempt_id);

public record ActionDto(
    float time_sec,
    string action,
    int? pos_x,
    int? pos_y
);

public record ActionsWrapperDto(
    ActionDto[] records
);

public record AttemptListItemDto(
    int attempt_id,
    int maze_seed,
    int maze_width,
    int maze_height,
    bool create_finish_area,
    bool create_finish_area_in_corner,
    DateTime created_at,
    float duration_sec,
    int? user_id,
    string? username
);

public record AttemptsListWrapper(
    AttemptListItemDto[] items
);

public record RegisterDto(
    string username,
    string email,
    string password,
    string? full_name
);

public record LoginDto(
    string login,
    string password
);

public record AuthResponseDto(
    bool success,
    string message,
    int? user_id,
    string? username,
    string? email,
    string? full_name
);