using UnityEngine;

public class HandMenuFacing : MonoBehaviour
{
    [Tooltip("Transform that defines the menu's 'front' (usually the canvas transform).")]
    public Transform menuFront;

    [Tooltip("Camera (AR camera). If null, uses Camera.main.")]
    public Camera cam;

    [Range(-1f, 1f)]
    public float showDotThreshold = -0.15f;

    [Tooltip("Optional: disable the whole canvas GameObject when hidden.")]
    public GameObject menuRoot;

    void Reset()
    {
        menuFront = transform;
        menuRoot = gameObject;
    }

    void Update()
    {
        Debug.Log("HandMenuFacingGate::LateUpdate - Running");
        if (!cam)
        {
            Debug.LogError("HandMenuFacingGate::LateUpdate - Camera Not Found!");
            cam = Camera.main;
        }
        if (!cam || !menuFront || !menuRoot) return;


        Vector3 toCam = (cam.transform.position - menuFront.position).normalized;

        float d = Vector3.Dot(menuFront.forward, toCam);

        bool shouldShow = showDotThreshold > d;

        Debug.Log($"HandMenuFacingGate::LateUpdate - d: {d}  --  shouldShow: {shouldShow}");
        menuRoot.SetActive(shouldShow);
    }
}