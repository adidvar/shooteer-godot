using Godot;
using System;

public partial class MovementComponent : Node
{
    [Export] public float Friction = 6f;
    [Export] public float MoveSpeed = 7.0f;
    [Export] public float GroundAccel = 14f;
    [Export] public float GroundDeaccel = 10f;
    [Export] public float AirAccel = 2.0f;
    [Export] public float JumpSpeed = 5f;
    [Export] public bool HoldJumpToBhop = true;

    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    private bool _wishJump = false;
    private CharacterBody3D _body;

    public void Setup(CharacterBody3D body)
    {
        _body = body;
    }

    public void ProcessMovement(Vector2 inputDir, bool isJumpPressed, bool isJumpJustPressed, bool isJumpJustReleased, bool inputsActive, float delta)
    {
        if (_body == null) return;

        Vector3 currentVelocity = _body.Velocity;

        if (inputsActive)
        {
            if (HoldJumpToBhop)
            {
                _wishJump = isJumpPressed;
            }
            else
            {
                if (isJumpJustPressed && !_wishJump)
                    _wishJump = true;
                if (isJumpJustReleased)
                    _wishJump = false;
            }
        }
        else
        {
            _wishJump = false;
        }

        Vector3 wishDir = (_body.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (_body.IsOnFloor())
        {
            GroundMove(ref currentVelocity, wishDir, delta);
        }
        else
        {
            AirMove(ref currentVelocity, wishDir, delta);
        }

        _body.Velocity = currentVelocity;
        _body.MoveAndSlide();
    }

    private void GroundMove(ref Vector3 velocity, Vector3 wishDir, float dt)
    {
        ApplyFriction(ref velocity, dt, !_wishJump);

        float wishSpeed = wishDir.LengthSquared() * MoveSpeed;
        Accelerate(ref velocity, wishDir, wishSpeed, GroundAccel, dt);

        if (_wishJump)
        {
            _wishJump = false;
            velocity.Y = JumpSpeed;
        }
    }

    private void AirMove(ref Vector3 velocity, Vector3 wishDir, float dt)
    {
        float wishSpeed = wishDir.LengthSquared() * MoveSpeed;
        Accelerate(ref velocity, wishDir, wishSpeed, AirAccel, dt);

        velocity.Y -= _gravity * dt;
    }

    private void ApplyFriction(ref Vector3 velocity, float dt, bool enabled)
    {
        if (!enabled) return;

        Vector3 vec = velocity;
        vec.Y = 0f;
        float lastSpeed = vec.Length();

        float drop = 0f;
        if (_body.IsOnFloor())
        {
            float control = lastSpeed < GroundDeaccel ? GroundDeaccel : lastSpeed;
            drop = control * Friction * dt;
        }

        float newSpeed = lastSpeed - drop;
        if (newSpeed < 0) newSpeed = 0;
        if (lastSpeed > 0) newSpeed /= lastSpeed;

        velocity.X *= newSpeed;
        velocity.Z *= newSpeed;
    }

    private void Accelerate(ref Vector3 velocity, Vector3 wishDir, float wishSpeed, float accel, float dt)
    {
        float currentSpeed = velocity.Dot(wishDir);
        float addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0) return;

        float accelSpeed = accel * dt * wishSpeed;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        velocity.X += accelSpeed * wishDir.X;
        velocity.Z += accelSpeed * wishDir.Z;
    }
}
