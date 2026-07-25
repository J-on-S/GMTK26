/// <summary>Supplies a signed travel speed (deg/sec) a follower orbits at. Sign = travel direction around the ring.</summary>
public interface ISpeedSource
{
    /// <summary>Current speed signed by the main travel direction.</summary>
    float GetSignedSpeed();
    void SetSignedSpeed(float value);

    void Disable();

    void Enable();

    
}
