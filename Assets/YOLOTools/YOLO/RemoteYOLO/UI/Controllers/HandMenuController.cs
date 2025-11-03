using MyBox;
using UnityEngine;
using UnityEngine.UI;

namespace YOLOTools.YOLO.RemoteYOLO.UI.Controllers
{
    public class HandMenuController : MonoBehaviour
    {

        [MustBeAssigned] [SerializeField] private RemoteYOLOHandler remoteYoloHandler;

        void Start()
        {
            gameObject.GetComponent<Toggle>().isOn = remoteYoloHandler.m_useCustomModel;
        }

        public async void OnCustomModelToggleChanged(Toggle toggle)
        {
            remoteYoloHandler.m_useCustomModel = toggle.isOn;

            if (toggle.isOn)
            {
                await remoteYoloHandler.UploadCustomModelAsync();
            }

            toggle.isOn = remoteYoloHandler.m_useCustomModel;
        }
    }
}
