using BepInEx.Logging;
using Comfort.Common;
using EFT;
using SAIN.Components;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AI;
using static SAIN.UserSettings.VisionConfig;
using SAIN.Helpers;

public class EnemyLineOfSightManager : MonoBehaviour
{
    private float SpherecastRadius = 0.05f;
    private LayerMask SightLayers => LayerMaskClass.HighPolyWithTerrainMask;
    private int MinJobSize = 1;
    private List<Player> RegisteredPlayers = Singleton<GameWorld>.Instance.RegisteredPlayers;
    private Dictionary<string, SAINComponent> SAINComponents = new Dictionary<string, SAINComponent>();
    private List<SAINComponent> SAINComponentsList = new List<SAINComponent>();

    private int Frames = 0;

    private void Awake()
    {
        Logger = BepInEx.Logging.Logger.CreateLogSource(GetType().Name);
    }

    private ManualLogSource Logger;

    private void Update()
    {
        var game = Singleton<IBotGame>.Instance;
        if (game == null)
        {
            return;
        }
        if (!EnableVisionJobs.Value)
        {
            return;
        }

        Frames++;
        if (Frames == CheckFrameCount.Value)
        {
            Frames = 0;

            FindBotComponents();
            GlobalRaycastJob();
            EnemyRaycastJob();
        }
    }

    private void FindBotComponents()
    {
        for (int i = 0; i < RegisteredPlayers.Count; i++)
        {
            var player = RegisteredPlayers[i];
            string Id = player.ProfileId;
            if (SAINComponents.ContainsKey(Id))
            {
                if (player.HealthController?.IsAlive == false || SAINComponents[Id] == null)
                {
                    SAINComponents.Remove(Id);
                }
            }
            else
            {
                if (player.HealthController?.IsAlive == true && player.IsAI)
                {
                    var bot = player.AIData?.BotOwner;
                    if (bot != null && bot.Profile.Info.Settings.Role != WildSpawnType.marksman)
                    {
                        SAINComponents.Add(Id, bot.GetOrAddComponent<SAINComponent>());
                    }
                }
            }
        }

        SAINComponentsList = SAINComponents.Values.ToList();
    }

    private Vector3 HeadPos(Player player)
    {
        return player.MainParts[BodyPartType.head].Position;
    }

    private Vector3 BodyPos(Player player)
    {
        return player.MainParts[BodyPartType.body].Position;
    }

    private Vector3[] Parts(Player player)
    {
        return player.MainParts.Values.Select(item => item.Position).ToArray();
    }

    private void EnemyRaycastJob()
    {
        List<SAINComponent> botsWithEnemy = new List<SAINComponent>();
        foreach (var bot in SAINComponentsList)
        {
            if (bot.Enemy != null)
            {
                botsWithEnemy.Add(bot);
            }
        }

        NativeArray<SpherecastCommand> spherecastCommands = new NativeArray<SpherecastCommand>(
            botsWithEnemy.Count * 6,
            Allocator.TempJob
        );
        NativeArray<RaycastHit> raycastHits = new NativeArray<RaycastHit>(
            botsWithEnemy.Count * 6,
            Allocator.TempJob
        );

        for (int i = 0; i < botsWithEnemy.Count; i++)
        {
            var bot = botsWithEnemy[i];
            Player enemy = bot.Enemy.EnemyPlayer;
            Vector3 head = HeadPos(bot.BotOwner.GetPlayer);
            var bodyParts = Parts(enemy);
            for (int j = 0; j < bodyParts.Length - 1; j++)
            {
                Vector3 target = bodyParts[j];
                Vector3 direction = target - head;
                float max = bot.BotOwner.Settings.Current.CurrentVisibleDistance;
                float rayDistance = Mathf.Clamp(direction.magnitude, 0f, max);
                spherecastCommands[j] = new SpherecastCommand(
                    head,
                    SpherecastRadius,
                    direction.normalized,
                    rayDistance,
                    SightLayers
                );
            }
        }

        JobHandle spherecastJob = SpherecastCommand.ScheduleBatch(
            spherecastCommands,
            raycastHits,
            MinJobSize
        );

        spherecastJob.Complete();
        int visiblecount = 0;

        for (int i = 0; i < botsWithEnemy.Count; i++)
        {
            bool visible = false;
            var bot = botsWithEnemy[i];
            Player enemy = bot.Enemy.EnemyPlayer;
            var bodyParts = Parts(enemy);
            for (int j = 0; j < bodyParts.Length - 1; j++)
            {
                if (raycastHits[j].collider == null)
                {
                    visiblecount++;
                    bot.Enemy?.OnGainSight();
                    if (DebugVision.Value)
                    {
                        DebugGizmos.SingleObjects.Line(bot.HeadPosition, bot.Enemy.EnemyPlayer.MainParts[BodyPartType.body].Position, Color.red, 0.01f, true, 0.1f, true);
                    }
                    break;
                }
            }
            if (!visible)
            {
                bot.Enemy?.OnLoseSight();
            }
        }

        if (DebugTimer < Time.time)
        {
            DebugTimer = Time.time + 5f;
            Logger.LogInfo($"Raycast Job Enemy Complete [{spherecastCommands.Length}] raycasts finished for [{botsWithEnemy.Count}] bots with enemy. Found [{visiblecount}] visible enemies.");
        }

        spherecastCommands.Dispose();
        raycastHits.Dispose();
    }

    private float DebugTimer = 0f;

    private void GlobalRaycastJob()
    {
        NativeArray<SpherecastCommand> allSpherecastCommands = new NativeArray<SpherecastCommand>(
            SAINComponentsList.Count * RegisteredPlayers.Count,
            Allocator.TempJob
        );
        NativeArray<RaycastHit> allRaycastHits = new NativeArray<RaycastHit>(
            SAINComponentsList.Count * RegisteredPlayers.Count,
            Allocator.TempJob
        );

        int currentIndex = 0;

        for (int i = 0; i < SAINComponentsList.Count; i++)
        {
            var bot = SAINComponentsList[i];
            Vector3 head = HeadPos(bot.BotOwner.GetPlayer);

            for (int j = 0; j < RegisteredPlayers.Count; j++)
            {
                Vector3 target = BodyPos(RegisteredPlayers[j]);
                Vector3 direction = target - head;
                float max = bot.BotOwner.Settings.Current.CurrentVisibleDistance;
                float rayDistance = Mathf.Clamp(direction.magnitude, 0f, max);

                allSpherecastCommands[currentIndex] = new SpherecastCommand(
                    head,
                    SpherecastRadius,
                    direction.normalized,
                    rayDistance,
                    SightLayers
                );

                currentIndex++;
            }
        }

        JobHandle spherecastJob = SpherecastCommand.ScheduleBatch(
            allSpherecastCommands,
            allRaycastHits,
            MinJobSize
        );
        int visiblecount = 0;
        spherecastJob.Complete();

        for (int i = 0; i < SAINComponentsList.Count; i++)
        {
            int startIndex = i * RegisteredPlayers.Count;
            var visiblePlayers = SAINComponentsList[i].VisiblePlayers;

            for (int j = 0; j < RegisteredPlayers.Count; j++)
            {
                currentIndex = startIndex + j;
                if (allRaycastHits[currentIndex].collider != null)
                {
                    if (visiblePlayers.Contains(RegisteredPlayers[j]))
                    {
                        visiblePlayers.Remove(RegisteredPlayers[j]);
                    }
                }
                else
                {
                    visiblecount++;
                    if (!visiblePlayers.Contains(RegisteredPlayers[j]))
                    {
                        visiblePlayers.Add(RegisteredPlayers[j]);
                    }
                }
            }
        }

        if (DebugTimer < Time.time)
        {
            Logger.LogInfo($"Raycast Job Complete [{allSpherecastCommands.Length}] raycasts finished. Found [{visiblecount}] visible players.");
        }

        allSpherecastCommands.Dispose();
        allRaycastHits.Dispose();
    }
}