﻿using EFT;
using Newtonsoft.Json;
using SAIN.Attributes;
using System.ComponentModel;
using System.Reflection;

namespace SAIN.Preset.BotSettings.SAINSettings.Categories
{
    public class SAINCoreSettings
    {
        [DefaultValue(160f)]
        [MinMax(45f, 180f)]
        public float VisibleAngle = 160f;

        [DefaultValue(150f)]
        [MinMax(50f, 500f)]
        [Advanced(AdvancedEnum.CopyValueFromEFT)]
        public float VisibleDistance = 150f;

        [DefaultValue(0.2f)]
        [MinMax(0.05f, 0.95f, 100f)]
        [Advanced(AdvancedEnum.IsAdvanced, AdvancedEnum.CopyValueFromEFT)]
        public float GainSightCoef = 0.2f;

        [DefaultValue(0.08f)]
        [MinMax(0.01f, 0.5f, 100f)]
        [Advanced(AdvancedEnum.IsAdvanced, AdvancedEnum.CopyValueFromEFT)]
        public float ScatteringPerMeter = 0.08f;

        [DefaultValue(0.12f)]
        [MinMax(0.01f, 0.5f, 100f)]
        [Advanced(AdvancedEnum.IsAdvanced, AdvancedEnum.CopyValueFromEFT)]
        public float ScatteringClosePerMeter = 0.12f;

        [NameAndDescription(
            "Injury Scatter Multiplier",
            "Increase scatter when a bots arms are injured.")]
        [DefaultValue(1.33f)]
        [MinMax(1f, 2f, 100f)]
        [Advanced(AdvancedEnum.IsAdvanced)]
        public float DamageCoeff = 1.33f;

        [NameAndDescription(
            "Audible Range Multiplier",
            "Modifies the distance that this bot can hear sounds")]
        [DefaultValue(1f)]
        [MinMax(0.1f, 3f, 100f)]
        public float HearingSense = 1f;

        [DefaultValue(true)]
        [Advanced(AdvancedEnum.Hidden)]
        public bool CanRun = true;

        [DefaultValue(true)]
        public bool CanGrenade = true;
    }
}
