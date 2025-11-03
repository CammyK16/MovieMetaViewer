using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YOLOTools.YOLO.RemoteYOLO.UI.Controllers
{
    public class LayersPerFrameSliderController : MonoBehaviour
    {

        [SerializeField] private TextMeshProUGUI textMeshProUGUI;

        [FormerlySerializedAs("remoteYoloHandler")] [SerializeField]
        private YOLOHandler yoloHandler;

        void Start()
        {
            gameObject.GetComponent<Slider>().value = yoloHandler._layersPerFrame;
            textMeshProUGUI.text = yoloHandler._layersPerFrame.ToString(CultureInfo.InvariantCulture);
        }

        public void OnConfidenceSliderValueChanged(Slider slider)
        {
            textMeshProUGUI.text = ((uint)slider.value).ToString(CultureInfo.InvariantCulture);
            yoloHandler._confidenceThreshold = (uint)slider.value;
        }
    }
}
