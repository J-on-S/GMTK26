using UnityEngine;
using System;

[CreateAssetMenu(fileName = "CamShakeEventChannel", menuName = "CamShakeEventChannel")]
public class CamShakeEventChannel : ScriptableObject
{
    public event Action<float,float,float> OnCamShake;

    public void Shake(float amplitude, float frequency, float duration) => OnCamShake?.Invoke(amplitude, frequency, duration);
}
