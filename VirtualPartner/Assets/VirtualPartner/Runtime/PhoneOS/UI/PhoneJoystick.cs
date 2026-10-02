using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public TMP_Text readout;
        public float radius = 42;
        public Vector2 Value { get; private set; }
        public event Action<Vector2> InputChanged;
        private int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || pointer != int.MinValue) return;
            pointer = data.pointerId; OnDrag(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out var point);
            SetValue(Vector2.ClampMagnitude(point / radius, 1));
        }
        public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) Release(); }
        public void Release() { pointer = int.MinValue; SetValue(Vector2.zero); }
        private void SetValue(Vector2 value)
        {
            Value = value; if (knob != null) knob.anchoredPosition = value * radius;
            if (readout != null) readout.text = $"X {value.x:0.00}   Y {value.y:0.00}";
            InputChanged?.Invoke(value);
        }
        private void OnDisable() => Release();
        private void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    }
}
