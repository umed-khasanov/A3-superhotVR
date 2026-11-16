using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

[RequireComponent(typeof(XRGrabInteractable))]
public class Throwable : MonoBehaviour
{
    [Header("Подсветка при наведении руки")]
    public Color normalColor = new Color(0.05f, 0.05f, 0.05f);
    public Color hoverColor  = new Color(0.35f, 0.35f, 0.35f);

    [Header("Возврат на стол после броска")]
    public float resetDelay = 4f;          

    XRGrabInteractable grab;
    Renderer rend;
    Rigidbody rb;
    Vector3 startPos;
    Quaternion startRot;
    HapticImpulsePlayer lastHaptics;        
    Coroutine resetRoutine;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rend = GetComponentInChildren<Renderer>();
        rb   = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;

        grab.hoverEntered.AddListener(_ => SetColor(hoverColor)); 
        grab.hoverExited.AddListener(_ => SetColor(normalColor));
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
        SetColor(normalColor);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (resetRoutine != null) StopCoroutine(resetRoutine);
        lastHaptics = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
        Pulse(0.3f, 0.05f);                 // лёгкий "щелчок": предмет в руке
    }

    void OnRelease(SelectExitEventArgs args)
    {
        resetRoutine = StartCoroutine(ResetAfterDelay());
    }

    // Вызывается из RedFigure при попадании
    public void Pulse(float amplitude, float duration)
    {
        if (lastHaptics != null) lastHaptics.SendHapticImpulse(amplitude, duration);
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        if (grab.isSelected) yield break;   
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(startPos, startRot);
    }

    void SetColor(Color c)
    {
        if (rend != null) rend.material.color = c;
    }
}
