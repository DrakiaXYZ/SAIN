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
        private static float finalExtractNavMeshSearchRadiusAddition = 0.5f;

        private GameObject DebugSphere = null;
        private ExfiltrationPoint ex = null;

        public ExtractPositionFinder(ExfiltrationPoint _ex)
        {
            ex = _ex;
        }

        public static GameObject CreateSphere(Vector3 position, float size, Color color)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Renderer>().material.color = color;
            sphere.GetComponent<Collider>().enabled = false;
            sphere.transform.position = position;
            sphere.transform.localScale = new Vector3(size, size, size);

            return sphere;
        }

        public void CreateDebugSphere(Color color)
        {
            if (!ExtractPosition.HasValue)
            {
                return;
            }

            if (DebugSphere == null)
            {
                DebugSphere = CreateSphere(ExtractPosition.Value, 0.5f, color);
            }

            DebugSphere.transform.position = ExtractPosition.Value;
            DebugSphere.GetComponent<Renderer>().material.color = color;
        }

        public void RemoveDebugSphere()
        {
            if (DebugSphere == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(DebugSphere);
            DebugSphere = null;
        }

        public IEnumerator SearchForExfilPosition()
        {
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
            }

            /*Vector3? nearestPlayerPosition = FindNearestPlayerPosition(ExtractPosition.Value);
            if (nearestPlayerPosition == null)
            {
                //if (SAINPlugin.DebugMode)
                    Logger.LogWarning($"Could not find a player position near {ex.Settings.Name}");
            }*/

            if 
            (
                (NearestSpawnPosition.HasValue && DoesCompletePathExist(NearestSpawnPosition.Value))
                //|| (nearestPlayerPosition.HasValue && DoesCompletePathExist(nearestPlayerPosition.Value))
            )
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
                    float distance = Vector3.Distance(ex.transform.position, point);
                    Logger.LogWarning($"Could not find a complete path to {ex.Settings.Name} from {point} ({distance}m away)");
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
                    Logger.LogWarning($"Could not find any NavMesh points for {ex.Settings.Name} from {colliderTestPoints.Count()} test points using radius {NavMeshSearchRadius}m");

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

            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            BotZone closestBotZone = botSpawnerClass.GetClosestZone(testPoint, out float dist);

            List<Vector3> navMeshPoints = new List<Vector3>();
            foreach(ISpawnPoint spawnPoint in closestBotZone.SpawnPoints)
            {
                Vector3? navMeshPoint = GetNearbyNavMeshPoint(spawnPoint.Position, 2);
                if (navMeshPoint.HasValue)
                {
                    navMeshPoints.Add(navMeshPoint.Value);
                }
            }

            IEnumerable<Vector3> sortedNavMeshPoints = navMeshPoints.OrderBy(x => Vector3.Distance(x, testPoint));
            if (!sortedNavMeshPoints.Any())
            {
                return null;
            }

            NearestSpawnPosition = sortedNavMeshPoints.First();

            return NearestSpawnPosition;
        }

        private Vector3? FindNearestPlayerPosition(Vector3 testPoint)
        {
            List<Player> allPlayers = Singleton<GameWorld>.Instance.AllAlivePlayersList;
            if (allPlayers.Count == 0)
            {
                return null;
            }

            return allPlayers.OrderBy(x => Vector3.Distance(testPoint, x.Position)).First().Position;
        }
    }
}
