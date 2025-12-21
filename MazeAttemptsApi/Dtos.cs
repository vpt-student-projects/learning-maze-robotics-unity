public record CreateAttemptDto(int maze_seed, int maze_width, int maze_height);
public record AttemptCreatedDto(int attempt_id);

public record ActionDto(float time_sec, string action, int? pos_x, int? pos_y);
public record ActionsWrapperDto(ActionDto[] records);
