﻿using EFT;
using JetBrains.Annotations;
using SAIN.Helpers;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.SubComponents.CoverFinder;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAIN.SAINComponent.Classes
{
    public enum CoverFinderState
    {
        off = 0,
        on = 1,
    }

    public class SAINCoverClass : BotBase, IBotClass
    {
        public event Action<CoverPoint> OnNewCoverInUse;

        public event Action<CoverPoint> OnEnterCover;

        public event Action OnSpottedInCover;

        public CoverPoint CoverInUse {
            get
            {
                return _coverInUse;
            }
            set
            {
                if (value != _coverInUse) {
                    _coverInUse = value;
                    OnNewCoverInUse?.Invoke(value);
                }
            }
        }

        public CoverPoint CoverPointImAt {
            get
            {
                var cover = CoverInUse;
                if (cover == null) {
                    return null;
                }
                if (checkMoving(cover) == true) {
                    return null;
                }
                return cover;
            }
        }

        private float _spottedTime;
        public bool SpottedInCover => _spottedTime > Time.time;

        public bool HasCover => CoverInUse != null;
        public bool InCover => HasCover && checkMoving(CoverInUse) == false;
        public bool IsMovingToCover => HasCover && checkMoving(CoverInUse) == true;
        public CoverFinderState CurrentCoverFinderState { get; private set; }
        public List<CoverPoint> CoverPoints => CoverFinder.CoverPoints;
        public CoverFinderComponent CoverFinder { get; private set; }
        public CoverPoint FallBackPoint => CoverFinder.FallBackPoint;
        public float LastHitInCoverTime { get; private set; }
        public float TimeSinceLastHitInCover => Time.time - LastHitInCoverTime;

        public SAINCoverClass(BotComponent bot) : base(bot)
        {
            CoverFinder = bot.GetOrAddComponent<CoverFinderComponent>();
        }

        public void Init()
        {
            base.SubscribeToPreset(null);
            CoverFinder.Init(Bot);
        }

        public void Update()
        {
            bool active = Bot.SAINLayersActive && Bot.Decision.HasDecision;
            ActivateCoverFinder(active);
            if (active) {
                checkEnterCover();
                createDebug();
            }
        }

        public void Dispose()
        {
            try {
                CoverFinder?.Dispose();
            }
            catch { }
        }

        public CoverPoint FindPointInDirection(Vector3 direction, float dotThreshold = 0.33f, float minDistance = 8f)
        {
            Vector3 botPosition = Bot.Position;
            for (int i = 0; i < CoverPoints.Count; i++) {
                CoverPoint point = CoverPoints[i];
                if (point != null &&
                    !point.Spotted &&
                    !point.CoverData.IsBad) {
                    Vector3 coverPosition = point.Position;
                    Vector3 directionToPoint = botPosition - coverPosition;

                    if (directionToPoint.sqrMagnitude > minDistance * minDistance
                        && Vector3.Dot(directionToPoint.normalized, direction.normalized) > dotThreshold) {
                        return point;
                    }
                }
            }
            return null;
        }

        private void checkEnterCover()
        {
            var coverImAt = CoverPointImAt;
            if (coverImAt == null) {
                _coverEntered = false;
                return;
            }

            if (!_coverEntered) {
                _coverEntered = true;
                OnEnterCover?.Invoke(coverImAt);
            }
        }

        private bool checkMoving([NotNull] CoverPoint cover)
        {
            // our straight distance to the cover position is less than the set threshold, we are in cover, so not moving to cover
            if (!isMovingTo(cover, cover.StraightDistanceStatus)) {
                return false;
            }
            // our path length is less than the set threshold, we are in cover, so not moving to cover
            if (!isMovingTo(cover, cover.PathDistanceStatus)) {
                return false;
            }
            return true;
        }

        private bool isMovingTo(CoverPoint point, CoverStatus status)
        {
            switch (status) {
                case CoverStatus.CloseToCover:
                case CoverStatus.MidRangeToCover:
                case CoverStatus.FarFromCover:
                    return true;

                default:
                    return false;
            }
        }

        private void createDebug()
        {
            if (SAINPlugin.DebugMode) {
                if (CoverInUse != null) {
                    if (debugCoverObject == null) {
                        debugCoverObject = DebugGizmos.CreateLabel(CoverInUse.Position, "Cover In Use");
                        debugCoverLine = DebugGizmos.Line(CoverInUse.Position, Bot.Position + Vector3.up, 0.075f, -1, true);
                    }
                    debugCoverObject.WorldPos = CoverInUse.Position;
                    DebugGizmos.UpdatePositionLine(CoverInUse.Position, Bot.Position + Vector3.up, debugCoverLine);
                }
            }
            else if (debugCoverObject != null) {
                DebugGizmos.DestroyLabel(debugCoverObject);
                debugCoverObject = null;
            }
        }

        public void GetHit(DamageInfo damageInfo, EBodyPart bodyPart, float floatVal)
        {
            if (InCover) {
                bool wasSpotted = CoverInUse.Spotted;
                LastHitInCoverTime = Time.time;
                CoverInUse.GetHit(damageInfo, bodyPart, Bot.Enemy);
                if (CoverInUse.Spotted && !wasSpotted) {
                    _spottedTime = Time.time + SpottedCoverPoint.SPOTTED_PERIOD;
                    OnSpottedInCover?.Invoke();
                }
            }
        }

        public void ActivateCoverFinder(bool value)
        {
            if (value) {
                CoverFinder?.LookForCover();
                CurrentCoverFinderState = CoverFinderState.on;
            }
            if (!value) {
                CoverFinder?.StopLooking();
                CurrentCoverFinderState = CoverFinderState.off;
            }
        }

        public void CheckResetCoverInUse()
        {
            CoverPoint coverInUse = Bot.Cover.CoverInUse;
            if (coverInUse != null && coverInUse.CoverData.IsBad) {
                Bot.Cover.CoverInUse = null;
                return;
            }

            ECombatDecision decision = Bot.Decision.CurrentCombatDecision;
            if (decision != ECombatDecision.MoveToCover
                && decision != ECombatDecision.RunToCover
                && decision != ECombatDecision.Retreat
                && decision != ECombatDecision.HoldInCover
                && decision != ECombatDecision.ShiftCover) {
                Bot.Cover.CoverInUse = null;
            }
        }

        public void SortPointsByPathDist()
        {
            CoverFinderComponent.OrderPointsByPathDist(CoverPoints);
        }

        public bool DuckInCover()
        {
            var point = CoverInUse;
            if (point != null) {
                var move = Bot.Mover;
                var prone = move.Prone;
                bool shallProne = prone.ShallProneHide();

                if (shallProne &&
                    (Bot.Decision.CurrentSelfDecision != ESelfDecision.None || Bot.Suppression.IsHeavySuppressed)) {
                    prone.SetProne(true);
                    return true;
                }
                if (move.Pose.SetPoseToCover()) {
                    return true;
                }
                if (shallProne &&
                    point.Collider.bounds.size.y < 0.85f) {
                    prone.SetProne(true);
                    return true;
                }
            }
            return false;
        }

        public bool CheckHasCoverFromEnemy(Enemy enemy)
        {
            return enemy != null &&
                enemy.InLineOfSight &&
                enemy.Vision.Angles.CanBeSeen &&
                enemy.CoverFromEnemy.VisionCover.HasCover;
        }

        public bool BotIsAtCoverPoint(CoverPoint coverPoint)
        {
            return coverPoint?.BotInThisCover == true;
        }

        public bool BotIsAtCoverInUse()
        {
            return CoverInUse?.BotInThisCover == true;
        }

        private bool _coverEntered;
        private GUIObject debugCoverObject;
        private GameObject debugCoverLine;
        private CoverPoint _coverInUse;
    }
}