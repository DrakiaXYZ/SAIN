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
        
        private static FieldInfo colliderField = AccessTools.Field(typeof(ExfiltrationPoint), "_collider");
        
        // If this is smaller than ~0.75m, no NavMesh points can be found for the Labs Vent extract. However, the larger it gets,
        // the more likely it is that a NavMesh point will be selected that's outside of the extract collider. 
        private static float finalExtractNavMeshSearchRadiusAddition = 0.75f;

        private static float initialNavMeshTestPointDensityFactor = 1f;
        private static float minNavMeshTestPointDensityFactor = 0.1f;
        private static float defaultExtractNavMeshSearchRadius = 3f;
        private static float maxExtractNavMeshSearchRadius = 5f;
        private static float pathEndpointHeightDeprioritizationFactor = 2;
        private static float minDistanceBetweenPathEndpoints = 75;
        private static int maxColliderTestPoints = 25;
        private static int maxPathEndpoints = 2;

        private ExfiltrationPoint ex = null;
        private readonly List<Vector3> pathEndpoints = new List<Vector3>();
        private readonly List<Vector3> sortedNavMeshPoints = new List<Vector3>();
        private readonly Stack<Vector3> navMeshTestPoints = new Stack<Vector3>();
        
        public IReadOnlyCollection<Vector3> PathEndpoints => pathEndpoints.AsReadOnly();

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

            FindExtractPositionsOnNavMesh();
            if (!navMeshTestPoints.Any())
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Cannot find any NavMesh positions for {ex.Settings.Name}");

                yield break;
            }

            ExtractPosition = navMeshTestPoints.Pop();
            //if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Testing point {ExtractPosition} for {ex.Settings.Name}. {navMeshTestPoints.Count} test points remaining.");

            FindPathEndPoints(ExtractPosition.Value);
            if (pathEndpoints.Count == 0)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find any path endpoints near {ex.Settings.Name}");

                yield break;
            }

            foreach (Vector3 pathEndPoint in pathEndpoints)
            {
                if (DoesCompletePathExistToExtractPoint(pathEndPoint))
                {
                    ValidPathFound = true;

                    //if (SAINPlugin.DebugMode)
                    Logger.LogInfo($"Found complete path to {ex.Settings.Name}");

                    yield break;
                }

                yield return null;
            }

            //if (SAINPlugin.DebugMode)
                Logger.LogWarning($"Could not find a complete path to {ex.Settings.Name}");
        }

        private bool DoesCompletePathExistToExtractPoint(Vector3 startingPoint)
        {
            if (!ExtractPosition.HasValue)
            {
                throw new InvalidOperationException("An extract position must be set before a path can be checked to it");
            }

            if (!NavMeshHelpers.DoesCompletePathExist(startingPoint, ExtractPosition.Value))
            {
                //if (SAINPlugin.DebugMode)
                {
                    float distanceBetweenPoints = Vector3.Distance(ex.transform.position, startingPoint);

                    Logger.LogWarning($"Could not find a complete path to {ex.Settings.Name} from {startingPoint} ({distanceBetweenPoints}m away).");
                }

                return false;
            }

            return true;
        }

        private float GetColliderTestPointSearchRadius(BoxCollider collider)
        {
            float searchRadius = Math.Min(Math.Min(collider.size.x, collider.size.y), collider.size.z) / 2;
            searchRadius = Math.Min(searchRadius, maxExtractNavMeshSearchRadius);
            if (searchRadius == 0)
            {
                searchRadius = defaultExtractNavMeshSearchRadius;

                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Collider size of {ex.Settings.Name} is (0, 0, 0). Using {searchRadius}m to check accessibility.");
            }

            return searchRadius;
        }

        private void FindExtractPositionsOnNavMesh()
        {
            if (navMeshTestPoints.Any())
            {
                return;
            }

            if (sortedNavMeshPoints.Any())
            {
                CreateNavMeshTestPointStack();
                return;
            }

            BoxCollider collider = (BoxCollider)colliderField.GetValue(ex);
            if (collider == null)
            {
                //if (SAINPlugin.DebugMode)
                Logger.LogWarning($"Could not find collider for {ex.Settings.Name}");

                return;
            }

            float searchRadius = GetColliderTestPointSearchRadius(collider);
            IEnumerable<Vector3> colliderTestPoints = GetColliderTestPoints(collider, searchRadius);
            IEnumerable<Vector3> navMeshPoints = GetColliderTestPointsOnNavMesh(colliderTestPoints, searchRadius + finalExtractNavMeshSearchRadiusAddition);
            List<Vector3> navMeshPointsToSort = navMeshPoints.ToArray().ToList();

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
            CreateNavMeshTestPointStack();
        }

        private void CreateNavMeshTestPointStack()
        {
            foreach (Vector3 testPoint in sortedNavMeshPoints)
            {
                navMeshTestPoints.Push(testPoint);
            }

            //if (SAINPlugin.DebugMode)
                Logger.LogInfo($"Found {navMeshTestPoints.Count} extract postions for {ex.Settings.Name}");
        }

        private IEnumerable<Vector3> GetColliderTestPoints(BoxCollider collider, float searchRadius)
        {
            float navNeshTestPointDensityFactor = initialNavMeshTestPointDensityFactor;
            IEnumerable<Vector3> colliderTestPoints = Enumerable.Repeat(Vector3.positiveInfinity, maxColliderTestPoints + 1);
            int lastPointCount = colliderTestPoints.Count();
            while ((lastPointCount > maxColliderTestPoints) && (navNeshTestPointDensityFactor >= minNavMeshTestPointDensityFactor))
            {
                colliderTestPoints = collider.GetNavMeshTestPoints(searchRadius, navNeshTestPointDensityFactor);
                if (colliderTestPoints.Count() == lastPointCount)
                {
                    //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not minimize collider test point count for {ex.Settings.Name}");

                    break;
                }

                lastPointCount = colliderTestPoints.Count();
                navNeshTestPointDensityFactor /= 2;
            }
            navNeshTestPointDensityFactor *= 2;

            if (!colliderTestPoints.Any())
            {
                colliderTestPoints = Enumerable.Repeat(collider.transform.position, 1);

                //if (SAINPlugin.DebugMode)
                Logger.LogWarning($"Could not create test points. Using collider position instead");
            }

            //if (SAINPlugin.DebugMode)
            {
                Logger.LogInfo($"Generated {colliderTestPoints.Count()} collider test points using a density factor of {Math.Round(navNeshTestPointDensityFactor, 3)} and a search radius of {searchRadius}m");
                Logger.LogInfo($"Extract collider: center={collider.transform.position}, size={collider.size}.");
            }

            return colliderTestPoints;
        }

        private IEnumerable<Vector3> GetColliderTestPointsOnNavMesh(IEnumerable<Vector3> colliderTestPoints, float searchRadius)
        {
            List<Vector3> navMeshPoints = new List<Vector3>();
            foreach (Vector3 testPoint in colliderTestPoints)
            {
                Vector3? navMeshPoint = NavMeshHelpers.GetNearbyNavMeshPoint(testPoint, searchRadius);
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
                    Logger.LogWarning($"Could not find any NavMesh points for {ex.Settings.Name} from {colliderTestPoints.Count()} test points using radius {searchRadius}m");
                    Logger.LogWarning($"Test points: {string.Join(",", colliderTestPoints)}");
                }

                return Enumerable.Empty<Vector3>();
            }

            return navMeshPoints;
        }

        private void FindPathEndPoints(Vector3 testPoint)
        {
            if (pathEndpoints.Count > 0)
            {
                return;
            }

            IEnumerable<Vector3> navMeshPoints = GetAllSpawnPointPositionsOnNavMesh();
            if (!navMeshPoints.Any())
            {
                return;
            }

            Dictionary<Vector3, float> navMeshPointDistances = navMeshPoints
                .ToDictionary(x => x, x => Vector3.Distance(x, testPoint) + (Math.Abs(x.y - testPoint.y) * pathEndpointHeightDeprioritizationFactor));

            for (int i = 0; i < maxPathEndpoints; i++)
            {
                IEnumerable<Vector3>  sortedNavMeshPoints = navMeshPoints
                    .Where(x => pathEndpoints.All(y => Vector3.Distance(x, y) > minDistanceBetweenPathEndpoints))
                    .OrderBy(x => navMeshPointDistances[x]);

                if (!sortedNavMeshPoints.Any())
                {
                    break;
                }

                pathEndpoints.Add(sortedNavMeshPoints.First());
            }
        }

        private static IEnumerable<Vector3> GetAllSpawnPointPositionsOnNavMesh()
        {
            List<Vector3> spawnPointPositions = new List<Vector3>();
            foreach (SpawnPointMarker spawnPointMarker in GameWorldHandler.SAINGameWorld.SpawnPointMarkers)
            {
                Vector3? spawnPointPosition = NavMeshHelpers.GetNearbyNavMeshPoint(spawnPointMarker.Position, 2);
                if (spawnPointPosition.HasValue && !spawnPointPositions.Contains(spawnPointPosition.Value))
                {
                    spawnPointPositions.Add(spawnPointPosition.Value);
                }
            }

            return spawnPointPositions;
        }
    }
}
