using UnityEngine;

public class HandMenuFacing : MonoBehaviour
{
    public Transform menuFront;

    public Camera cam;

    [Range(-1f, 1f)]
    public float showDotThreshold = -0.15f;

    public GameObject menuRoot;

    void Reset()
    {
        menuFront = transform;
        menuRoot = gameObject;
    }

    void Update()
    {
        if (!cam)
        {
            cam = Camera.main;
        }
        if (!cam || !menuFront || !menuRoot) return;


        Vector3 toCam = (cam.transform.position - menuFront.position).normalized;

        float d = Vector3.Dot(menuFront.forward, toCam);

        bool shouldShow = showDotThreshold > d;

        menuRoot.SetActive(shouldShow);
    }
}