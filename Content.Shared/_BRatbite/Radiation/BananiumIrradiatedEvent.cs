namespace Content.Shared._BRatbite.Radiation;

public readonly record struct BananiumIrradiatedEvent(float FrameTime, float RadsPerSecond)
{
    public readonly float FrameTime = FrameTime;

    public readonly float RadsPerSecond = RadsPerSecond;

    public float TotalRads => RadsPerSecond * FrameTime;
}
