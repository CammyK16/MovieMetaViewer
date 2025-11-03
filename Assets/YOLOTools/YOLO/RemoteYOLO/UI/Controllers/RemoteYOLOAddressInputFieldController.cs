using MyBox;
using TMPro;
using UnityEngine;

namespace YOLOTools.YOLO.RemoteYOLO.UI.Controllers
{
    public class RemoteYOLOAddressInputFieldController : MonoBehaviour
    {

        [MustBeAssigned] [SerializeField] private RemoteYOLOHandler remoteYoloHandler;

        void Start()
        {
            gameObject.GetComponent<TMP_InputField>().text = remoteYoloHandler.m_remoteYOLOProcessorAddress;
        }

        public void OnEndEdit(TMP_InputField inputField)
        {
            remoteYoloHandler.m_remoteYOLOClient.BaseAddress = inputField.text;
        }
    }
}
