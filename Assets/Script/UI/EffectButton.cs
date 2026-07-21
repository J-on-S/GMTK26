using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class EffectButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
    public TextMeshProUGUI textMesh;
    public Transform targetTransform;

    public bool scaleText;
    public bool scaleObject;

    public float scaleMultiplier = 1.3f;
    public float duration = 0.2f;

    private Vector3 textOriginal;
    private Vector3 objOriginal;

    private Coroutine routine;

    void Start() {
        if (textMesh) textOriginal = textMesh.transform.localScale;
        if (targetTransform) objOriginal = targetTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData _) => StartScale(true);
    public void OnPointerExit(PointerEventData _) => StartScale(false);

    public void OnPointerClick(PointerEventData _) {

    }

    void StartScale(bool bigger) {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ScaleTo(bigger ? scaleMultiplier : 1f));
    }

    IEnumerator ScaleTo(float m) {
        float t = 0f;

        Vector3 startText = scaleText && textMesh ? textMesh.transform.localScale : Vector3.zero;
        Vector3 startObj = scaleObject && targetTransform ? targetTransform.localScale : Vector3.zero;

        Vector3 targetText = scaleText && textMesh ? textOriginal * m : Vector3.zero;
        Vector3 targetObj = scaleObject && targetTransform ? objOriginal * m : Vector3.zero;

        while (t < duration) {
            float p = t / duration;

            if (scaleText && textMesh)
                textMesh.transform.localScale = Vector3.Lerp(startText, targetText, p);

            if (scaleObject && targetTransform)
                targetTransform.localScale = Vector3.Lerp(startObj, targetObj, p);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (scaleText && textMesh)
            textMesh.transform.localScale = targetText;

        if (scaleObject && targetTransform)
            targetTransform.localScale = targetObj;
    }
}
