﻿using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using SAIN.Components.BotController;
using SAIN.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SAIN.Components
{
    public class SAINGameworldComponent : MonoBehaviour
    {
        private void Awake()
        {
            SAINBotController = this.GetOrAddComponent<SAINBotControllerComponent>();
            ExtractFinder = this.GetOrAddComponent<Extract.ExtractFinderComponent>();
        }

        private void Update()
        {
            //SAINMainPlayer = ComponentHelpers.AddOrDestroyComponent(SAINMainPlayer, GameWorld?.MainPlayer);

            findSpawnPointMarkers();
        }

        private void OnDestroy()
        {
            try
            {
                ComponentHelpers.DestroyComponent(SAINBotController);
                ComponentHelpers.DestroyComponent(SAINMainPlayer);
            }
            catch
            {
                Logger.LogError("Dispose Component Error");
            }
        }

        private void findSpawnPointMarkers()
        {
            if ((SpawnPointMarkers != null) || (Camera.main == null))
            {
                return;
            }

            SpawnPointMarkers = UnityEngine.Object.FindObjectsOfType<SpawnPointMarker>();

            if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Found {SpawnPointMarkers.Length} spawn point markers");
        }

        public GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public SAINMainPlayerComponent SAINMainPlayer { get; private set; } = null;
        public SAINBotControllerComponent SAINBotController { get; private set; } = null;
        public Extract.ExtractFinderComponent ExtractFinder { get; private set; } = null;
        public SpawnPointMarker[] SpawnPointMarkers { get; private set; } = null;
    }

}
