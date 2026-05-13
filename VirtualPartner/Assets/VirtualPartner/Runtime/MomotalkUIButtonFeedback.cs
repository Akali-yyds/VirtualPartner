using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualPartner.Runtime
{
    public sealed class MomotalkUIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.94f;
        [SerializeField] private float releaseSpeed = 18f;

        private Vector3 baseScale;
        private bool pressed;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (pressed)
                return;

            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.unscaledDeltaTime * releaseSpeed);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            transform.localScale = baseScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pressed = false;
        }
    }
}
