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
        public Stack<Vector3> NavMeshTestPoints { get; private set; } = new Stack<Vector3>();
        
        private static FieldInfo colliderField = AccessTools.Field(typeof(ExfiltrationPoint), "_collider");
        private static float defaultExtractNavMeshSearchRadius = 3f;
        private static float maxExtractNavMeshSearchRadius = 3f;

        // If this is smaller than ~0.75m, no NavMesh points can be found for the Labs Vent extract. However, the larger it gets,
        // the more likely it is that a NavMesh point will be selected that's outside of the extract collider. 
        private static float finalExtractNavMeshSearchRadiusAddition = 0.75f;

        private ExfiltrationPoint ex = null;

        public ExtractPositionFinder(ExfiltrationPoint _ex)
        {
            ex = _ex;
        }

        public IEnumerator SearchForExfilPosition()
        {
            if (ValidPathFound)
            {
                yield break;
            }

            if (ex == null)
            {
                if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Exfil is null in list!");

                yield break;
            }

            if (!TryFindExtractPositionsOnNavMesh())
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Cannot find any NavMesh positions for {ex.Settings.Name}");

                yield break;
            }

            ExtractPosition = NavMeshTestPoints.Pop();
            //if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Testing point {ExtractPosition} for {ex.Settings.Name}. {NavMeshTestPoints.Count} test points remaining.");

            FindNearestSpawnPointPosition(ExtractPosition.Value);
            if (NearestSpawnPosition == null)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find a spawn position near {ex.Settings.Name}");

                yield break;
            }

            if (DoesCompletePathExist(NearestSpawnPosition.Value))
            {
                ValidPathFound = true;

                //if (SAINPlugin.DebugMode)
                    Logger.LogInfo($"Found complete path to {ex.Settings.Name}");

                yield break;
            }

            //if (SAINPlugin.DebugMode)
                Logger.LogWarning($"Could not find a complete path to {ex.Settings.Name}");
        }

        private bool DoesCompletePathExist(Vector3 point)
        {
            if (!ExtractPosition.HasValue)
            {
                throw new InvalidOperationException("An extract position must be set before a path can be checked to it");
            }

            if (!NavMeshHelpers.DoesCompletePathExist(point, ExtractPosition.Value))
            {
                //if (SAINPlugin.DebugMode)
                {
                    float distanceBetweenPoints = Vector3.Distance(ex.transform.position, point);

                    Logger.LogWarning($"Could not find a complete path to {ex.Settings.Name} from {point} ({distanceBetweenPoints}m away).");
                }

                return false;
            }

            return true;
        }

        private bool TryFindExtractPositionsOnNavMesh()
        {
            if (NavMeshTestPoints.Any())
            {
                return true;
            }

            BoxCollider collider = (BoxCollider)colliderField.GetValue(ex);
            if (collider == null)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find collider for {ex.Settings.Name}");

                return false;
            }

            NavMeshSearchRadius = Math.Min(Math.Min(collider.size.x, collider.size.y), collider.size.z) / 2;
            NavMeshSearchRadius = Math.Min(NavMeshSearchRadius, maxExtractNavMeshSearchRadius);
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

            NavMeshSearchRadius += finalExtractNavMeshSearchRadiusAddition;

            List<Vector3> navMeshPoints = new List<Vector3>();
            foreach (Vector3 testPoint in colliderTestPoints)
            {
                Vector3? navMeshPoint = GetNearbyNavMeshPoint(testPoint, NavMeshSearchRadius);
                if (navMeshPoint == null)
                {
                    continue;
                }

                if (navMeshPoints.Any(x => x == navMeshPoint))
                {
                    continue;
                }

                navMeshPoints.Add(navMeshPoint.Value);
            }

            if (!navMeshPoints.Any())
            {
                //if (SAINPlugin.DebugMode)
                {
                    Logger.LogWarning($"Could not find any NavMesh points for {ex.Settings.Name} from {colliderTestPoints.Count()} test points using radius {NavMeshSearchRadius}m");
                    Logger.LogWarning($"Extract collider: center={collider.transform.position}, size={collider.size}.");
                    Logger.LogWarning($"Test points: {string.Join(",", colliderTestPoints)}");
                }

                return false;
            }

            List<Vector3> navMeshPointsToSort = navMeshPoints.ToArray().ToList();
            List<Vector3> sortedNavMeshPoints = new List<Vector3>();
            Vector3 referencePoint = ex.transform.position;
            bool chooseFirst = true;
            while (navMeshPointsToSort.Count > 0)
            {
                IEnumerable<Vector3> tmpSortedNavMeshPoints = navMeshPointsToSort.OrderBy(x => Vector3.Distance(x, referencePoint));

                referencePoint = chooseFirst ? tmpSortedNavMeshPoints.First() : tmpSortedNavMeshPoints.Last();
                chooseFirst = !chooseFirst;

                sortedNavMeshPoints.Add(referencePoint);
                navMeshPointsToSort.Remove(referencePoint);
            }

            sortedNavMeshPoints.Reverse();
            foreach (Vector3 testPoint in sortedNavMeshPoints)
            {
                NavMeshTestPoints.Push(testPoint);
            }

            //if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Found {NavMeshTestPoints.Count} extract postions for {ex.Settings.Name} using search radius {NavMeshSearchRadius}m");

            return true;
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

            List<Vector3> navMeshPoints = new List<Vector3>();
            foreach(SpawnPointMarker spawnPointMarker in GameWorldHandler.SAINGameWorld.SpawnPointMarkers)
            {
                Vector3? navMeshPoint = GetNearbyNavMeshPoint(spawnPointMarker.Position, 2);
                if (navMeshPoint.HasValue)
                {
                    navMeshPoints.Add(navMeshPoint.Value);
                }
            }

            float heightDeprioritizationFactor = 5;
            IEnumerable<Vector3> sortedNavMeshPoints = navMeshPoints.OrderBy(x => Vector3.Distance(x, testPoint) + (Math.Abs(x.y - testPoint.y) * heightDeprioritizationFactor));
            if (!sortedNavMeshPoints.Any())
            {
                return null;
            }

            NearestSpawnPosition = sortedNavMeshPoints.First();

            return NearestSpawnPosition;
        }
    }
}
