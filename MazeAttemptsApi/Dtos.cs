using System;
public record CreateAttemptDto(int maze_seed, int maze_width, int maze_height, bool create_finish_area, bool create_finish_area_in_corner);

public record AttemptCreatedDto(int attempt_id);

public record ActionDto(float time_sec, string action, int? pos_x, int? pos_y);
public record ActionsWrapperDto(ActionDto[] records);



public record AttemptListItemDto(
    int attempt_id,
    int maze_seed,
    int maze_width,
    int maze_height,
    bool create_finish_area,
    bool create_finish_area_in_corner,
    DateTime created_at,
    float duration_sec
);


public record AttemptsListWrapper(AttemptListItemDto[] items);

