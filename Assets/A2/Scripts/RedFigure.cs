using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public class RedFigure : MonoBehaviour
{
    [Header("Движение")]
    public float walkSpeed = 0.3f;      
    public float stopDistance = 1.5f;   

    [Header("Попадание")]
    public float minHitSpeed = 2f;      

    [Header("Осколки")]
    public int shardCount = 14;
    public float shardSize = 0.12f;
    public float explosionForce = 3f;
    public float shardLifetime = 3f;

    [Header("Звук и повтор")]
    public AudioClip shatterClip;
    public float respawnDelay = 2f;

    Transform player;
    Rigidbody rb;
    AudioSource audioSrc;
    Renderer[] rends;
    Collider[] cols;
    Vector3 startPos;
    bool broken;

    void Start()
    {
        player   = Camera.main.transform;  
        rb       = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        audioSrc = GetComponent<AudioSource>();
        audioSrc.spatialBlend = 1f;         
        rends = GetComponentsInChildren<Renderer>();
        cols  = GetComponentsInChildren<Collider>();
        startPos = transform.position;
    }

    void FixedUpdate()
    {
        if (broken) return;
        // Идём к игроку только по полу (высоту не меняем)
        Vector3 target = new Vector3(player.position.x, transform.position.y, player.position.z);
        if (Vector3.Distance(transform.position, target) > stopDistance)
            rb.MovePosition(Vector3.MoveTowards(transform.position, target, walkSpeed * Time.fixedDeltaTime));
        transform.LookAt(target);
    }

    void OnCollisionEnter(Collision hit)
    {
        if (broken) return;
        Throwable item = hit.collider.GetComponentInParent<Throwable>();
        if (item == null) return;                              
        if (hit.relativeVelocity.magnitude < minHitSpeed) return; 

        Shatter(hit.GetContact(0).point);
        item.Pulse(0.8f, 0.15f);                                
    }

    [Header("Удар кулаком")]
    public float minPunchSpeed = 1.5f;  

    
    void OnTriggerEnter(Collider other)
    {
        if (broken) return;
        HandHitter fist = other.GetComponent<HandHitter>();
        if (fist == null) return;
        if (fist.Speed < minPunchSpeed) return;               

        Shatter(other.transform.position);
        fist.Pulse(0.9f, 0.15f);                              
    }

    void Shatter(Vector3 hitPoint)
    {
        broken = true;
        if (shatterClip != null) audioSrc.PlayOneShot(shatterClip);

        Material mat = rends.Length > 0 ? rends[0].material : null;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.transform.position = transform.position + Random.insideUnitSphere * 0.4f + Vector3.up * Random.Range(0.3f, 1.6f);
            s.transform.rotation = Random.rotation;
            s.transform.localScale = Vector3.one * shardSize * Random.Range(0.5f, 1.2f);
            if (mat != null) s.GetComponent<Renderer>().material = mat;
            Rigidbody srb = s.AddComponent<Rigidbody>();
            srb.AddExplosionForce(explosionForce, hitPoint, 2f, 0.3f, ForceMode.Impulse);
            Destroy(s, shardLifetime);
        }

        SetVisible(false);
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        transform.position = startPos;
        SetVisible(true);
        broken = false;
    }

    void SetVisible(bool on)
    {
        foreach (var r in rends) r.enabled = on;
        foreach (var c in cols)  c.enabled = on;
    }
}
