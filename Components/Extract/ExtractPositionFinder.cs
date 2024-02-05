using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using EFT.Interactive;
using EFT.UI;
using HarmonyLib;
using SAIN.Helpers;
using SAIN.SAINComponent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.Components.Extract
{
    public class ExtractPositionFinder
    {
        public bool ValidPathFound { get; private set; } = false;
        public Vector3? ExtractPosition { get; private set; } = null;
        public Vector3? NearestSpawnPosition { get; private set; } = null;
        public float NavMeshSearchRadius { get; private set; } = 0;
        public Vector3[] NavMeshTestPoints { get; private set; } = new Vector3[0];

        private static FieldInfo colliderField = AccessTools.Field(typeof(ExfiltrationPoint), "_collider");
        private static float defaultExtractNavMeshSearchRadius = 3f;

        private ExfiltrationPoint ex;

        public ExtractPositionFinder(ExfiltrationPoint _ex)
        {
            ex = _ex;
        }

        public IEnumerator SearchForExfilPosition()
        {
            if (ex == null)
            {
                if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Exfil is null in list!");

                yield break;
            }

            FindExtractPosition();
            if (ExtractPosition == null)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Cannot find NavMesh position for {ex.Settings.Name}");

                yield break;
            }

            FindNearestSpawnPointPosition(ExtractPosition.Value);
            if (NearestSpawnPosition == null)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find a spawn position near {ex.Settings.Name}");

                yield break;
            }

            if (!NavMeshHelpers.DoesCompletePathExist(NearestSpawnPosition.Value, ExtractPosition.Value))
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find a complete path to {ex.Settings.Name}");

                yield break;
            }

            ValidPathFound = true;

            //if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Found complete path to {ex.Settings.Name}");
        }

        private Vector3? FindExtractPosition()
        {
            if (ExtractPosition != null)
            {
                return ExtractPosition;
            }

            BoxCollider collider = (BoxCollider)colliderField.GetValue(ex);
            if (collider == null)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find collider for {ex.Settings.Name}");

                return null;
            }

            NavMeshSearchRadius = Math.Min(Math.Min(collider.size.x, collider.size.y), collider.size.z) / 2;
            if (NavMeshSearchRadius == 0)
            {
                NavMeshSearchRadius = defaultExtractNavMeshSearchRadius;

                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Collider size of {ex.Settings.Name} is (0, 0, 0). Using {NavMeshSearchRadius}m to check accessibility.");
            }

            IEnumerable<Vector3> colliderTestPoints = collider.GetNavMeshTestPoints(NavMeshSearchRadius, 2);
            if (!colliderTestPoints.Any())
            {
                colliderTestPoints = Enumerable.Repeat(collider.transform.position, 1);

                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not create test points. Using collider position instead");
            }

            NavMeshSearchRadius += 0.5f;

            List<Vector3> navMeshPoints = new List<Vector3>();
            foreach (Vector3 testPoint in colliderTestPoints)
            {
                Vector3? navMeshPoint = GetNearbyNavMeshPoint(testPoint, NavMeshSearchRadius);
                if (navMeshPoint == null)
                {
                    continue;
                }

                navMeshPoints.Add(navMeshPoint.Value);
            }

            NavMeshTestPoints = navMeshPoints.ToArray();

            IEnumerable<Vector3> sortedNavMeshPoints = navMeshPoints.OrderBy(x => Vector3.Distance(x, ex.transform.position));
            if (!sortedNavMeshPoints.Any())
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find any NavMesh points for {ex.Settings.Name} from {colliderTestPoints.Count()} test points using radius {NavMeshSearchRadius}m");

                return null;
            }

            ExtractPosition = sortedNavMeshPoints.First();

            //if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Found extract postion {ExtractPosition} for {ex.Settings.Name} using search radius {NavMeshSearchRadius}m");

            return ExtractPosition;
        }

        private static Vector3? GetNearbyNavMeshPoint(Vector3 testPoint, float radius)
        {
            if (NavMesh.SamplePosition(testPoint, out var hit, radius, -1))
            {
                return hit.position;
            }

            return null;
        }

        private Vector3? FindNearestSpawnPointPosition(Vector3 testPoint)
        {
            if (NearestSpawnPosition != null)
            {
                return NearestSpawnPosition;
            }

            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            BotZone closestBotZone = botSpawnerClass.GetClosestZone(testPoint, out float dist);

            IEnumerable<ISpawnPoint> sortedSpawnPoints = closestBotZone.SpawnPoints.OrderBy(x => Vector3.Distance(x.Position, testPoint));
            if (!sortedSpawnPoints.Any())
            {
                return null;
            }

            NearestSpawnPosition = sortedSpawnPoints.First().Position;

            return NearestSpawnPosition;
        }
    }
}
