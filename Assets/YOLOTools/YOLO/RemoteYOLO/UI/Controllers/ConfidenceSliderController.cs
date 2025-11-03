using System;
using System.Globalization;
using MyBox;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace YOLOTools.YOLO.RemoteYOLO.UI.Controllers
{
    public class ConfidenceSliderController : MonoBehaviour
    {

        [SerializeField] private TextMeshProUGUI textMeshProUGUI;
        [MustBeAssigned] [SerializeField] private RemoteYOLOHandler remoteYoloHandler;

        void Start()
        {
            gameObject.GetComponent<Slider>().value = (float)Math.Round(remoteYoloHandler.m_confidenceThreshold, 2);
            if (textMeshProUGUI) textMeshProUGUI.text = Math.Round(remoteYoloHandler.m_confidenceThreshold, 2)
                .ToString(CultureInfo.InvariantCulture);
        }

        public void OnConfidenceSliderValueChanged(Slider slider)
        {
            if (textMeshProUGUI) textMeshProUGUI.text = Math.Round(slider.value, 2).ToString(CultureInfo.InvariantCulture);
            remoteYoloHandler.m_confidenceThreshold = (float)Math.Round(slider.value, 2);
        }
    }
}
