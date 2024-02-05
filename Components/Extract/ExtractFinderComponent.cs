using Comfort.Common;
using EFT;
using EFT.Interactive;
using SAIN.SAINComponent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

namespace SAIN.Components.Extract
{
    public class ExtractFinderComponent : MonoBehaviour
    {
        public bool IsFindingExtracts { get; private set; } = false;

        private ExfiltrationPoint[] AllExfils;
        private ExfiltrationPoint[] AllScavExfils;
        private Dictionary<ExfiltrationPoint, Vector3> ValidExfils = new Dictionary<ExfiltrationPoint, Vector3>();
        private Dictionary<ExfiltrationPoint, Vector3> ValidScavExfils = new Dictionary<ExfiltrationPoint, Vector3>();
        private Dictionary<ExfiltrationPoint, ExtractPositionFinder> extractPositionFinders = new Dictionary<ExfiltrationPoint, ExtractPositionFinder>();
        private float CheckExtractTimer = 0f;

        public void Update()
        {
            if (!GetExfilControl())
            {
                return;
            }

            if (CheckExtractTimer > Time.time)
            {
                return;
            }

            CheckExtractTimer = Time.time + 20f;

            if (!IsFindingExtracts)
            {
                // This should be done regularly because the method checks if bots can path to each extract. However, it needs to be done
                // in a coroutine to minimize the performance impact
                StartCoroutine(FindAllExfils());
            }
        }

        public void OnDisable()
        {
            StopAllCoroutines();
        }

        public int CountValidExfilsForBot(SAINComponentClass bot)
        {
            return GetValidExfilsForBot(bot).Count;
        }

        public IDictionary<ExfiltrationPoint, Vector3> GetValidExfilsForBot(SAINComponentClass bot)
        {
            return bot.Info.Profile.IsScav ? ValidScavExfils : ValidExfils;
        }

        private bool GetExfilControl()
        {
            if (Singleton<AbstractGame>.Instance?.GameTimer == null)
            {
                return false;
            }

            ExfiltrationControllerClass ExfilController = Singleton<GameWorld>.Instance.ExfiltrationController;
            if (ExfilController == null)
            {
                return false;
            }

            AllExfils = ExfilController.ExfiltrationPoints;
            if (SAINPlugin.DebugMode && AllExfils != null)
            {
                Logger.LogInfo($"Found {AllExfils?.Length} possible Exfil Points in this map.");
            }

            AllScavExfils = ExfilController.ScavExfiltrationPoints;
            if (SAINPlugin.DebugMode && AllScavExfils != null)
            {
                Logger.LogInfo($"Found {AllScavExfils?.Length} possible Scav Exfil Points in this map.");
            }

            return true;
        }

        private IEnumerator FindAllExfils()
        {
            IsFindingExtracts = true;

            yield return UpdateValidExfils(ValidExfils, AllExfils);
            yield return UpdateValidExfils(ValidScavExfils, AllScavExfils);

            IsFindingExtracts = false;
        }

        private IEnumerator UpdateValidExfils(IDictionary<ExfiltrationPoint, Vector3> validExfils, ExfiltrationPoint[] allExfils)
        {
            if (allExfils == null)
            {
                yield break;
            }

            foreach (var ex in allExfils)
            {
                if (validExfils.ContainsKey(ex))
                {
                    continue;
                }

                ExtractPositionFinder finder = GetExtractPositionSearchJob(ex);
                yield return finder.SearchForExfilPosition();

                if (!finder.ValidPathFound)
                {
                    continue;
                }

                validExfils.Add(ex, finder.ExtractPosition.Value);
            }
        }

        private ExtractPositionFinder GetExtractPositionSearchJob(ExfiltrationPoint ex)
        {
            if (extractPositionFinders.ContainsKey(ex))
            {
                return extractPositionFinders[ex];
            }

            ExtractPositionFinder job = new ExtractPositionFinder(ex);
            extractPositionFinders.Add(ex, job);

            return job;
        }
    }
}
