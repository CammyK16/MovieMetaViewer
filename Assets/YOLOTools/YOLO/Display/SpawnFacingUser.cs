using UnityEngine;

public class SpawnFacingUser : MonoBehaviour
{
    [Header("Where to spawn relative to users head")]
    public Transform head;

    public void FaceUser()
    {
        if (!head) head = Camera.main != null ? Camera.main.transform : null;
        if (!head)
        {
            Debug.Log("SpawnFacingUser::FaceUser - Dinnae work");
            return;
        }

        Vector3 toHead = transform.position - head.position;
        toHead = Vector3.ProjectOnPlane(toHead, Vector3.up).normalized;

        if (toHead.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(toHead, Vector3.up);
    }
}
