using UnityEngine;

public class CameraSetupManager : MonoBehaviour
{
    void Start()
    {
        OVRCameraRig cameraRig = GetComponent<OVRCameraRig>();
        foreach (var camera in cameraRig.GetComponentsInChildren<Camera>())
        {
            EnablePassthrough(camera);
        }
    }

    private void EnablePassthrough(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
    }
}
