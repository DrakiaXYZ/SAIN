﻿using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using EFT.Communications;
using UnityEngine;
using static Mono.Security.X509.X520;
using SAIN.Helpers;
using EFT.UI;

namespace SAIN
{
    internal static class Logger
    {
        public static void LogInfo(object data) 
            => Log(LogLevel.Info, data);
        public static void LogDebug(object data) 
            => Log(LogLevel.Debug, data);
        public static void LogWarning(object data) 
            => Log(LogLevel.Warning, data);
        public static void LogError(object data) 
            => Log(LogLevel.Error, data);

        public static void NotifyInfo(object data, ENotificationDurationType duration = ENotificationDurationType.Default) 
            => NotifyMessage(data, duration, ENotificationIconType.Note);
        public static void NotifyDebug(object data, ENotificationDurationType duration = ENotificationDurationType.Default) 
            => NotifyMessage(data, duration, ENotificationIconType.Note, Color.gray);
        public static void NotifyWarning(object data, ENotificationDurationType duration = ENotificationDurationType.Default) 
            => NotifyMessage(data, duration, ENotificationIconType.Alert, Color.yellow);
        public static void NotifyError(object data, ENotificationDurationType duration = ENotificationDurationType.Long) 
            => NotifyMessage(data, duration, ENotificationIconType.Alert, Color.red, true);

        public static void NotifyMessage(object data, 
            ENotificationDurationType durationType = ENotificationDurationType.Default,
            ENotificationIconType iconType = ENotificationIconType.Default,
            UnityEngine.Color? textColor = null, bool Error = false)
        {
            if (_nextNotification < Time.time && SAINPlugin.DebugMode)
            {
                _nextNotification = Time.time + 0.25f;
                string message = Error ? CreateErrorMessage(data) : data.ToString();
                NotificationManagerClass.DisplayMessageNotification(message, durationType, iconType, textColor);
            }
        }

        private static string CreateErrorMessage(object data)
        {
            StackTrace stackTrace = new StackTrace();
            int max = Mathf.Clamp(stackTrace.FrameCount, 0, 10);
            for (int i = 0; i < max; i++)
            {
                MethodBase method = stackTrace.GetFrame(i)?.GetMethod();
                Type type = method?.DeclaringType;
                if (type != null && type.DeclaringType != typeof(Logger))
                {
                    string errorString = $"[{type} : {method}]: ERROR: {data}";
                    return errorString;
                }
            }
            return data.ToString();
        }

        private static void Log(LogLevel level, object data)
        {
            string methodsString = string.Empty;
            Type declaringType = null;

            if (level != LogLevel.Debug)
            {
                int max = GetMaxFrames(level);
                StackTrace stackTrace = new StackTrace(2);
                max = Mathf.Clamp(max, 0, stackTrace.FrameCount);
                for (int i = 0; i < max; i++)
                {
                    var method = stackTrace.GetFrame(i).GetMethod();

                    if (method.DeclaringType == typeof(Logger)) continue;

                    if (declaringType == null)
                    {
                        declaringType = method.DeclaringType;
                    }

                    if (!methodsString.IsNullOrEmpty())
                    {
                        methodsString = "." + methodsString;
                    }

                    methodsString = $"{method.Name}()" + methodsString;
                }
                methodsString = $"[{methodsString}]:";
            }

            string result = $"[{declaringType}] : [{methodsString}] : [{data}]";

            if (SAINLogger == null)
            {
                SAINLogger = BepInEx.Logging.Logger.CreateLogSource("SAIN");
            }
            if (level == LogLevel.Error || level == LogLevel.Fatal)
            {
                //NotifyError(data);
                if (MonoBehaviourSingleton<PreloaderUI>.Instance?.Console != null)
                {
                    //ConsoleScreen.LogError(data.ToString());
                }
            }
            SAINLogger.Log(level, result);
        }

        private static float _nextNotification;

        private static int GetMaxFrames(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info: 
                    return 1;
                case LogLevel.Warning: 
                    return 2;
                case LogLevel.Error: 
                    return 3;
                case LogLevel.Fatal: 
                    return 4;
                default: 
                    return 1;
            }
        }

        private static ManualLogSource SAINLogger;
    }
}