using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;


[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class HandHitter : MonoBehaviour
{
    public float pushMinSpeed = 0.6f;   
    public float pushStrength = 1.2f;  

    public float Speed => velocity.magnitude;
    public Vector3 Velocity => velocity;

    Vector3 velocity;
    Vector3 lastPos;
    HapticImpulsePlayer haptics;

    void Start()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;           
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;          
        haptics = GetComponentInParent<HapticImpulsePlayer>();
        lastPos = transform.position;
    }

    void Update()
    {
        
        Vector3 v = (transform.position - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        velocity = Vector3.Lerp(velocity, v, 0.5f);
        lastPos = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic) return;            
        var grab = rb.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;          
        if (Speed < pushMinSpeed) return;

        rb.AddForce(velocity * pushStrength, ForceMode.VelocityChange);
        Pulse(0.4f, 0.05f);                                   
    }

    public void Pulse(float amplitude, float duration)
    {
        if (haptics != null) haptics.SendHapticImpulse(amplitude, duration);
    }
}
