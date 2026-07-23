using UnityEngine;
using System.Collections;

public class CamShake : MonoBehaviour
{
    public CamShakeEventChannel eventChannel;

    private float startTime;

    private void OnEnable()
    {
        eventChannel.OnCamShake += Shake;
    }

    private void OnDisable()
    {
        eventChannel.OnCamShake -= Shake;
    }

    private void Shake(float amp, float freq, float dur)
    {
        startTime = Time.time;
        StartCoroutine(ShakeCoroutine(amp, freq, dur));
    }

    private IEnumerator ShakeCoroutine(float amp, float freq, float dur)
    {
        while (Time.time < startTime+dur)
        {
            transform.position = new Vector3(Mathf.Sin(freq*(Time.time - startTime)) * amp, 0, -10);
            yield return null;
        }

        transform.position = Vector3.back * 10;
    }
}
