using System;
using System.Reflection;
using UnityEngine;
using LanguageTutor.Core;

namespace LanguageTutor.UI
{
    public class TalkButtonListeningHighlight : MonoBehaviour
    {
        private Component _animatorOverrideLayerWeight;
        private MethodInfo _setOverrideLayerActive;

        private void Awake()
        {
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
            NPCController.OnListeningStateChanged += HandleListeningStateChanged;
            HandleListeningStateChanged(false);
        }

        private void OnDisable()
        {
            NPCController.OnListeningStateChanged -= HandleListeningStateChanged;
        }

        private void HandleListeningStateChanged(bool isListening)
        {
            if (_animatorOverrideLayerWeight != null && _setOverrideLayerActive != null)
            {
                _setOverrideLayerActive.Invoke(_animatorOverrideLayerWeight, new object[] { isListening });
            }
        }
    }
}
