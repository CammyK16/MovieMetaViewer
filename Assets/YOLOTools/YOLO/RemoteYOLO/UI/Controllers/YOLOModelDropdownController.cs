using MyBox;
using TMPro;
using UnityEngine;

namespace YOLOTools.YOLO.RemoteYOLO.UI.Controllers
{
    public class YOLOModelDropdownController : MonoBehaviour
    {

        [MustBeAssigned] [SerializeField] private RemoteYOLOHandler remoteYoloHandler;
    
    
        void Start()
        {
            gameObject.GetComponent<TMP_Dropdown>().value = (int)remoteYoloHandler.m_YOLOModel;
        }

        public void OnYOLOModelDropdownValueChanged(TMP_Dropdown dropdown)
        {
            remoteYoloHandler.m_YOLOModel = (YOLOModel)dropdown.value;
        }
    }
}
