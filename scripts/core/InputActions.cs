using Godot;

/// <summary>
/// The input actions from the project settings, made once. Passing a plain string where Godot wants a StringName,
/// as in Input.IsActionPressed("pause"), makes a new StringName on every call, which is garbage every frame.
/// </summary>
public static class InputActions
{
	public static readonly StringName MoveLeft = "move_left";
	public static readonly StringName MoveRight = "move_right";
	public static readonly StringName MoveUp = "move_up";
	public static readonly StringName MoveDown = "move_down";
	public static readonly StringName MenuSelect = "menu_select";
	public static readonly StringName Pause = "pause";
}
