using Godot;

/// <summary>
/// An arrow at the edge of the screen that points toward the gateway.
/// Drawn as an arrowhead with a small ring behind it, echoing the gateway's shape.
/// </summary>
public partial class GatewayIndicator : Control
{
	public CameraRig Camera;
	public Gateway Gateway;

	private bool _show;
	private Vector2 _tip;
	private Vector2 _direction = Vector2.Up;

	public override void _Ready()
	{
		// Run after the camera has moved this frame.
		ProcessPriority = UpdateOrder.Hud;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		if (Camera == null || Gateway == null)
			return;

		Vector2 screen = Size;
		Vector2 center = screen * 0.5f;
		float margin = Tuning.IndicatorEdgeMargin;
		var insetArea = new Rect2(margin, margin, screen.X - margin * 2f, screen.Y - margin * 2f);

		Vector3 gatewayWorld = Gateway.GlobalPosition;
		bool gatewayOnScreen = !Camera.IsPositionBehind(gatewayWorld)
			&& insetArea.HasPoint(Camera.UnprojectPosition(gatewayWorld));

		_show = Tuning.IndicatorAlwaysVisible || !gatewayOnScreen;
		if (_show)
		{
			// Find the on-screen direction by projecting a nearby point on the line from the
			// screen center toward the gateway. Every point on that line lands on the same
			// screen direction, and a nearby one is always in front of the camera, which a
			// very distant gateway may not be.
			Vector2 toGateway = Gateway.PlanePosition - PlaneMath.ToPlane(Camera.Focus);
			float distance = toGateway.Length();
			if (distance > 0.01f)
			{
				float probeDistance = Mathf.Min(distance, Tuning.CameraHeight * 0.25f);
				Vector3 probe = Camera.Focus + PlaneMath.ToWorld(toGateway / distance) * probeDistance;
				Vector2 onScreen = Camera.UnprojectPosition(probe) - center;
				if (onScreen.LengthSquared() > 0.0001f)
					_direction = onScreen.Normalized();
			}

			// Slide along that direction until the inset edge of the screen is reached.
			Vector2 half = insetArea.Size * 0.5f;
			float reachX = Mathf.Abs(_direction.X) > 0.0001f ? half.X / Mathf.Abs(_direction.X) : float.MaxValue;
			float reachY = Mathf.Abs(_direction.Y) > 0.0001f ? half.Y / Mathf.Abs(_direction.Y) : float.MaxValue;
			_tip = center + _direction * Mathf.Min(reachX, reachY);
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!_show)
			return;

		float size = Tuning.IndicatorSize;
		Color color = Tuning.GatewayColor;
		Vector2 side = new(-_direction.Y, _direction.X);
		Vector2 back = _tip - _direction * size;
		Vector2 notch = _tip - _direction * size * 0.65f;

		// A notched arrowhead, drawn as two halves, so the pointing end is unmistakable.
		DrawColoredPolygon(new[] { _tip, back + side * size * 0.5f, notch }, color);
		DrawColoredPolygon(new[] { _tip, notch, back - side * size * 0.5f }, color);
		DrawArc(_tip - _direction * size * 1.75f, size * 0.5f, 0f, Mathf.Tau, 32, color, size * 0.14f, true);
	}
}
