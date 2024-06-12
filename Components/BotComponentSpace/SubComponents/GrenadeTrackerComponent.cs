﻿using EFT;
using UnityEngine;

namespace SAIN.SAINComponent.SubComponents
{
    public class GrenadeTracker
    {
        public GrenadeTracker(BotComponent bot, Grenade grenade, Vector3 dangerPoint, float reactionTime)
        {
            Bot = bot;
            ReactionTime = reactionTime;
            DangerPoint = dangerPoint;
            Grenade = grenade;
            if ((grenade.transform.position - bot.Position).magnitude < 10f)
            {
                setSpotted();
            }
        }

        private readonly BotComponent Bot;
        private BotOwner BotOwner => Bot.BotOwner;

        public float GrenadeDistance { get; private set; }

        public void Update()
        {
            if (BotOwner == null || BotOwner.IsDead || Grenade == null || _sentToBot)
            {
                return;
            }

            if (!_sentToBot && CanReact)
            {
                _sentToBot = true;
                BotOwner.BewareGrenade.AddGrenadeDanger(DangerPoint, Grenade);
                Bot.Talk.GroupSay(EPhraseTrigger.OnEnemyGrenade, ETagStatus.Combat, false, 80);
                return;
            }

            GrenadeDistance = (Grenade.transform.position - BotOwner.Position).magnitude;
            if (GrenadeDistance < 5f)
            {
                setSpotted();
            }

            if (!_spotted &&
                _nextCheckRaycastTime < Time.time)
            {
                _nextCheckRaycastTime = Time.time + 0.05f;
                if (checkVisibility())
                {
                    setSpotted();
                }
            }
        }

        private bool _sentToBot;

        private void setSpotted()
        {
            if (!_spotted)
            {
                _timeSpotted = Time.time;
                _spotted = true;
            }
        }

        private bool checkVisibility()
        {
            Vector3 grenadePos = Grenade.transform.position + (Vector3.up * 0.1f);

            if (!BotOwner.LookSensor.IsPointInVisibleSector(grenadePos))
            {
                return false;
            }

            Vector3 headPos = BotOwner.LookSensor._headPoint;
            Vector3 grenadeDir = grenadePos - headPos;

            return !Physics.Raycast(headPos, grenadeDir, grenadeDir.magnitude, LayerMaskClass.HighPolyWithTerrainMaskAI);
        }

        private float _timeSpotted { get; set; }
        public float TimeSinceSpotted => _spotted ? Time.time - _timeSpotted : 0f;
        public Grenade Grenade { get; private set; }
        public Vector3 DangerPoint { get; private set; }
        private bool _spotted { get; set; }
        public bool CanReact => _spotted && TimeSinceSpotted > ReactionTime;

        private readonly float ReactionTime;
        private float _nextCheckRaycastTime;
    }
}