﻿using EFT;
using SAIN.Helpers;
using SAIN.Preset.GlobalSettings;
using SAIN.SAINComponent;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAIN.Components.PlayerComponentSpace
{
    public class PlayerIlluminationClass : PlayerComponentBase
    {
        public event Action<bool> OnPlayerIlluminationChanged;

        public bool Illuminated => TimeSinceIlluminated <= ILLUMINATED_BUFFER_PERIOD;
        public float Level { get; private set; }
        public float TimeSinceIlluminated => Time.time - _timeLastIlluminated;

        private const float ILLUMINATED_BUFFER_PERIOD = 0.5f;

        private float _timeLastIlluminated;

        public PlayerIlluminationClass(PlayerComponent playerComponent) : base(playerComponent)
        {
        }

        public void Init()
        {
        }

        public void Update()
        {
        }

        public void Dispose()
        {
        }

        public void SetIllumination(bool value, float level, LightTrigger trigger, float sqrMagnitude)
        {
            updateLightsDictionary(value, level, trigger);
        }

        public void SetIllumination(float level, float time)
        {
            if (_resetLevelTime < time) {
                _resetLevelTime = time + 0.05f;
                Level = 0;
            }
            if (level > Level) {
                Level = level;
            }
            _timeLastIlluminated = time;
        }

        private float _resetLevelTime;
        private float _nextCheckRaycastTime;
        private const float RAYCAST_FREQ = 0.25f;
        private const float RAYCAST_FREQ_PERF_MODE = 0.5f;

        private bool checkIfLightsInRange(out float illumLevel)
        {
            illumLevel = 0;
            if (_lightsInRange.Count == 0) {
                return false;
            }
            foreach (var light in _lightsInRange) {
                if (light.Value > illumLevel) {
                    illumLevel = light.Value;
                }
            }
            return true;
        }

        private void updateLightsDictionary(bool value, float level, LightTrigger trigger)
        {
            bool inList = _lightsInRange.ContainsKey(trigger);
            if (value) {
                if (inList) {
                    _lightsInRange[trigger] = level;
                }
                else {
                    _lightsInRange.Add(trigger, level);
                }
                return;
            }

            if (!value &&
                inList) {
                _lightsInRange.Remove(trigger);
            }
        }

        private readonly Dictionary<LightTrigger, float> _lightsInRange = new Dictionary<LightTrigger, float>();
    }
}