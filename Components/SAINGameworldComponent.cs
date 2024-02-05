using Comfort.Common;
using EFT;
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

        public GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public SAINMainPlayerComponent SAINMainPlayer { get; private set; }
        public SAINBotControllerComponent SAINBotController { get; private set; }
        public Extract.ExtractFinderComponent ExtractFinder { get; private set; }
    }

}
