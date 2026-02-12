using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace LanguageTutor.UI
{
    public class SlowModeToggleVisualSync : MonoBehaviour
    {
        private Toggle _toggle;
        private Component _animatorOverrideLayerWeight;
        private MethodInfo _setOverrideLayerActive;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _animatorOverrideLayerWeight = GetComponent("AnimatorOverrideLayerWeigth");

            if (_animatorOverrideLayerWeight != null)
            {
                _setOverrideLayerActive = _animatorOverrideLayerWeight.GetType().GetMethod(
                    "SetOverrideLayerActive",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(bool) },
                    null);
            }
        }

        private void OnEnable()
        {
            if (_toggle == null)
            {
                return;
            }

            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
            OnToggleValueChanged(_toggle.isOn);
        }

        private void OnDisable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            }
        }

        private void OnToggleValueChanged(bool isOn)
        {
            if (_animatorOverrideLayerWeight != null && _setOverrideLayerActive != null)
            {
                _setOverrideLayerActive.Invoke(_animatorOverrideLayerWeight, new object[] { isOn });
            }
        }
    }
}
