using UnityEngine;

public class StandUprightController : MonoBehaviour
{
    [Header("Stand Up")]
    [SerializeField] private float defaultDuration = 0.3f;
    [SerializeField] private bool freezeRotationDuringStandUp = true;
    [SerializeField] private bool zeroLinearVelocityOnStart = true;
    [SerializeField] private bool zeroAngularVelocityOnStart = true;

    private Coroutine activeRoutine;

    public System.Collections.IEnumerator StandUprightRoutine(float duration)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        activeRoutine = StartCoroutine(StandUprightCoro(duration <= 0f ? defaultDuration : duration));
        yield return activeRoutine;
    }

    public void StandUprightImmediate()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (zeroLinearVelocityOnStart) rb.linearVelocity = Vector2.zero;
            if (zeroAngularVelocityOnStart) rb.angularVelocity = 0f;
        }
        transform.rotation = Quaternion.identity;
    }

    private System.Collections.IEnumerator StandUprightCoro(float duration)
    {
        Transform t = transform;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        bool hadRb = rb != null;
        bool prevFreeze = false;

        if (hadRb)
        {
            prevFreeze = rb.freezeRotation;
            if (freezeRotationDuringStandUp) rb.freezeRotation = true;
            if (zeroLinearVelocityOnStart) rb.linearVelocity = Vector2.zero;
            if (zeroAngularVelocityOnStart) rb.angularVelocity = 0f;
        }

        Quaternion start = t.rotation;
        Quaternion target = Quaternion.identity;

        if (duration <= 0f)
        {
            t.rotation = target;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = Mathf.Clamp01(elapsed / duration);
                t.rotation = Quaternion.Slerp(start, target, s);
                yield return null;
            }
            t.rotation = target;
        }

        if (hadRb && freezeRotationDuringStandUp)
        {
            rb.freezeRotation = prevFreeze;
        }

        activeRoutine = null;
    }
}


