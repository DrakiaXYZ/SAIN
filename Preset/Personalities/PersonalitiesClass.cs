﻿using EFT;
using Newtonsoft.Json;
using SAIN.Attributes;
using SAIN.Helpers;
using SAIN.SAINComponent.Classes.Info;
using System.Collections.Generic;

namespace SAIN.Preset.Personalities
{
    public sealed class PersonalitySettingsClass
    {
        [JsonConstructor]
        public PersonalitySettingsClass()
        { }

        public PersonalitySettingsClass(SAINPersonality personality, string name, string description)
        {
            SAINPersonality = personality;
            Name = name;
            Description = description;
        }

        public SAINPersonality SAINPersonality;
        public string Name;
        public string Description;

        public PersonalityVariablesClass Variables = new PersonalityVariablesClass();

        public Dictionary<WildSpawnType, BotType> AllowedBotTypes = new Dictionary<WildSpawnType, BotType>();

        public bool CanBePersonality(SAINBotInfoClass infoClass)
        {
            return CanBePersonality(infoClass.WildSpawnType, infoClass.PowerLevel, infoClass.PlayerLevel);
        }
        public bool CanBePersonality(WildSpawnType wildSpawnType, float PowerLevel, int PlayerLevel)
        {
            if (Variables.Enabled == false)
            {
                return false;
            }
            if (Variables.CanBeRandomlyAssigned && EFTMath.RandomBool(Variables.RandomlyAssignedChance))
            {
                return true;
            }
            if (!AllowedBotTypes.ContainsKey(wildSpawnType))
            {
                return false;
            }
            if (PowerLevel > Variables.PowerLevelMax || PowerLevel < Variables.PowerLevelMin)
            {
                return false;
            }
            if (PlayerLevel > Variables.MaxLevel)
            {
                return false;
            }
            return EFTMath.RandomBool(Variables.RandomChanceIfMeetRequirements);
        }

        public class PersonalityVariablesClass
        {
            [Advanced(AdvancedEnum.Hidden)]
            const string PowerLevelDescription = " Power level is a combined number that takes into account armor, the class of that armor, and the weapon class that is currently used by a bot." +
                " Power Level usually falls within 30 to 120 on average, and almost never goes above 150";

            [Name("Personality Enabled")]
            [Description("Enables or Disables this Personality, if a All Chads, All GigaChads, or AllRats is enabled in global settings, this value is ignored")]
            [Default(true)]
            public bool Enabled = true;

            [NameAndDescription("Can Be Randomly Assigned", "A percentage chance that this personality can be applied to any bot, regardless of bot stats, power, player level, or anything else.")]
            [Default(true)]
            public bool CanBeRandomlyAssigned = true;

            [NameAndDescription("Randomly Assigned Chance", "If personality can be randomly assigned, this is the chance that will happen")]
            [Default(3)]
            [MinMax(0, 100)]
            public float RandomlyAssignedChance = 3;

            [NameAndDescription("Max Level", "The max level that a bot can be to be eligible for this personality.")]
            [Default(100)]
            [MinMax(1, 100)]
            public float MaxLevel = 100;

            [NameAndDescription("Random Chance If Meets Requirements", "If the bot meets all conditions for this personality, this is the chance the personality will actually be assigned.")]
            [Default(60)]
            [MinMax(0, 100, 1)]
            public float RandomChanceIfMeetRequirements = 60;

            [NameAndDescription("Power Level Minimum", "Minimum Power level for a bot to use this personality." + PowerLevelDescription)]
            [Default(0)]
            [MinMax(0, 250, 1)]
            public float PowerLevelMin = 0;

            [NameAndDescription("Power Level Maximum", "Maximum Power level for a bot to use this personality." + PowerLevelDescription)]
            [Default(250)]
            [MinMax(0, 250, 1)]
            public float PowerLevelMax = 250;

            [Default(1f)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public float HoldGroundBaseTime = 1f;

            [Default(0.66f)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public float HoldGroundMinRandom = 0.66f;

            [Default(1.5f)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public float HoldGroundMaxRandom = 1.5f;

            [Default(45)]
            public float SearchBaseTime = 45;

            [Default(1f)]
            public float AggressionMultiplier = 1f;

            [Default(false)]
            public bool CanJumpCorners = false;

            [Default(false)]
            public bool CanTaunt = false;

            [Default(false)]
            public bool Sneaky = false; 

            [Default(0.0f)]
            [Percentage0to1]
            public float SneakySpeed = 0.0f; 

            [Default(0.0f)]
            [Percentage0to1]
            public float SneakyPose = 0.0f;

            [Default(true)]
            public bool CanAmbush = true;

            [Default(25f)]
            [Percentage]
            public float AmbushChance = 25f;

            [Default(5f)]
            [MinMax(0.5f, 60f, 10)]
            public float CheckAmbushFrequency = 5f;

            [Default(1f)]
            [Percentage0to1]
            public float BaseSearchMoveSpeed = 1f;

            [Default(false)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public bool FrequentTaunt = false;

            [Default(false)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public bool ConstantTaunt = false;

            [Default(true)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public bool CanRespondToVoice = true;

            [Default(20f)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            [Percentage]
            public float TauntFrequency = 20f;

            [Default(20f)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            [Percentage]
            public float TauntMaxDistance = 20f;

            [Default(false)]
            public bool SprintWhileSearch = false;

            [Advanced(AdvancedEnum.IsAdvanced)]
            [Default(false)]
            public bool FrequentSprintWhileSearch = false;

            [Default(false)]
            public bool CanRushEnemyReloadHeal = false;

            [Default(false)]
            [Advanced(AdvancedEnum.IsAdvanced)]
            public bool CanFakeDeathRare = false;
        }
    }
}