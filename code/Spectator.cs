using Sandbox;
using System;

public sealed class Spectator : Component
{
	[Property]
	GameObject Projectile;

	const float MOVE_SPEED = 2000;
	const float CAM_SENSITIVITY = 1;

	float CameraPitch = 0;
	float CameraYaw = 0;

	protected override void OnUpdate()
	{
		var move_wish = Input.AnalogMove;

		CameraYaw += Input.AnalogLook.yaw * CAM_SENSITIVITY;
		CameraPitch += Input.AnalogLook.pitch * CAM_SENSITIVITY;
		CameraPitch = Math.Min(CameraPitch,80);
		CameraPitch = Math.Max(CameraPitch,-80);

		var camera_rot = Rotation.FromYaw(CameraYaw) * Rotation.FromPitch(CameraPitch);
		WorldRotation = camera_rot;

		WorldPosition += MOVE_SPEED * (camera_rot * move_wish) * Time.Delta;
		//Log.Info("~ "+move_wish);

		if (Input.Pressed("attack1")) {
			var p = Projectile.Clone();
			p.WorldPosition = WorldPosition;
		}
	}
}
