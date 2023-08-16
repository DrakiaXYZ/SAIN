﻿using BepInEx;
using Comfort.Common;
using EFT;
using EFT.Console.Core;
using EFT.UI;
using SAIN.Attributes;
using System;
using UnityEngine;
using static SAIN.Editor.RectLayout;
using static SAIN.Editor.SAINLayout;
using static SAIN.Editor.Sounds;
using ColorsClass = SAIN.Editor.Util.ColorsClass;

namespace SAIN.Editor
{
    public static class SAINEditor
    {
        static SAINEditor()
        {
            ConsoleScreen.Processor.RegisterCommand("saineditor", new Action(ToggleGUI));
            ConsoleScreen.Processor.RegisterCommand("pausegame", new Action(TogglePause));
        }

        public static void Init()
        {
            CursorSettings.InitCursor();
        }

        public static bool GameIsPaused { get; private set; }
        public static bool PauseOnEditorOpen;
        public static bool AdvancedBotConfigs;

        [ConsoleCommand("Toggle SAIN GUI Editor")]
        private static void ToggleGUI()
        {
            DisplayingWindow = !DisplayingWindow;
        }

        private static float CheckKeyLimiter;

        private static void TogglePause()
        {
            Player mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
            if (mainPlayer?.HandsAnimator != null)
            {
                GameIsPaused = !GameIsPaused;
                if (GameIsPaused)
                {
                    ConsoleScreen.Log("Pausing Game...");

                    Time.timeScale = 0f;
                    mainPlayer.HandsAnimator.SetAnimationSpeed(0f);
                }
                else
                {
                    Time.timeScale = 1f;
                    mainPlayer.HandsAnimator.SetAnimationSpeed(1f);
                }
            }
            else
            {
                GameIsPaused = false;
            }
        }

        public static bool ShiftKeyPressed;
        public static bool CtrlKeyPressed;
        private static bool ToggleKeyPressed;
        private static bool PauseKeyPressed;
        private static bool EscapeKeyPressed;

        private static void CheckKeys()
        {
            if (CheckKeyLimiter < Time.time)
            {
                CheckKeyLimiter = Time.time + 0.1f;
                ShiftKeyPressed = Input.GetKey(KeyCode.LeftShift);
                CtrlKeyPressed = Input.GetKey(KeyCode.LeftControl);
                ToggleKeyPressed = Input.GetKeyDown(SAINPlugin.OpenEditorConfigEntry.Value.MainKey);
                PauseKeyPressed = Input.GetKeyDown(SAINPlugin.PauseConfigEntry.Value.MainKey);
                EscapeKeyPressed = Input.GetKeyDown(KeyCode.Escape);
            }
        }

        public static void Update()
        {
            if (DisplayingWindow)
            {
                CursorSettings.SetUnlockCursor(0, true);
            }
            else
            {
                CheckKeys();
            }

            if (SAINPlugin.PauseConfigEntry.Value.IsDown())
            {
                TogglePause();
            }
            if ((SAINPlugin.OpenEditorConfigEntry.Value.IsDown() && !DisplayingWindow) || SAINPlugin.OpenEditorButton.Value)
            {
                if (SAINPlugin.OpenEditorButton.Value)
                {
                    SAINPlugin.OpenEditorButton.BoxedValue = false;
                    SAINPlugin.OpenEditorButton.Value = false;
                }
                ToggleGUI();
            }
        }

        public static void LateUpdate()
        {
            if (DisplayingWindow) CursorSettings.SetUnlockCursor(0, true);
        }

        public static void OnGUI()
        {
            if (DisplayingWindow)
            {
                if (!CacheCreated)
                {
                    CacheCreated = true;
                    ColorsClass.CreateCache();
                    TexturesClass.CreateCache();
                    StylesClass.CreateCache();
                }

                CursorSettings.SetUnlockCursor(0, true);
                GUIUtility.ScaleAroundPivot(ScaledPivot, Vector2.zero);
                MainWindow = GUI.Window(0, MainWindow, MainWindowFunc, "SAIN AI Settings Editor", GetStyle(Style.window));
                UnityInput.Current.ResetInputAxes();
            }
        }

        private static bool CacheCreated;

        private static void MainWindowFunc(int TWCWindowID)
        {
            GUI.FocusWindow(TWCWindowID);

            CheckKeys();

            if (ToggleKeyPressed || EscapeKeyPressed)
            {
                ToggleGUI();
                return;
            }

            CreateDragBar();

            CreateTopBarOptions();

            EEditorTab selectedTab = EditTabsClass.TabSelectMenu(25f, 3f, 0.5f);

            float space = DragRect.height + EditTabsClass.TabMenuRect.height + 5;
            Space(space);

            if (!string.IsNullOrEmpty(ExceptionString))
            {
                BeginHorizontal();
                BuilderClass.Alert(ExceptionString, "EXPORT ERROR",
                    40f, 1000, ColorNames.MidRed);
                BuilderClass.Alert(
                    "LogOutput is located in Bepinex folder in SPT install. GitHub Link is available on the SAIN mod Page, right hand side.",
                    "Please Report Issue to SAIN Github along with your LogOutput File",
                    40f, 1000, ColorNames.MidRed);
                EndHorizontal();
            }

            GUITabs.CreateTabs(selectedTab);

            DrawTooltip();
        }

        private static void CreateDragBar()
        {
            GUI.DrawTexture(DragRect, DragBackgroundTexture, ScaleMode.StretchToFill, true, 0);
            GUI.Box(DragRect, $"SAIN GUI Editor | Preset: {SAINPlugin.LoadedPreset.Info.Name}", GetStyle(Style.dragBar));
            GUI.DragWindow(DragRect);
        }

        public static string ExceptionString = string.Empty;

        private static void CreateTopBarOptions()
        {
            var style = GetStyle(Style.button);
            if (GUI.Button(SaveAllRect, new GUIContent("Save All Changes", $"Export All Changes to SAIN/Presets/{SAINPlugin.LoadedPreset.Info.Name}"), style))
            {
                PlaySound(EUISoundType.InsuranceInsured);
                SAINPlugin.LoadedPreset.ExportAll();
            }
            if (GUI.Toggle(PauseRect, PauseOnEditorOpen, "Pause When Open", style))
            {
                PauseOnEditorOpen = !PauseOnEditorOpen;
                PlaySound(EUISoundType.ButtonClick);
            }
            if (GUI.Button(ExitRect, "X", style))
            {
                PlaySound(EUISoundType.MenuEscape);
                ToggleGUI();
            }
        }

        private static void DrawTooltip()
        {
            string tooltip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tooltip))
            {
                var currentEvent = Event.current;

                const int width = 200;

                var ToolTipStyle = GetStyle(Style.tooltip);
                var height = ToolTipStyle.CalcHeight(new GUIContent(tooltip), width) + 10;

                var x = currentEvent.mousePosition.x;
                var y = currentEvent.mousePosition.y + 15;

                if (x > Screen.width / 3)
                {
                    x -= width;
                }

                GUI.Box(new Rect(x, y, width, height), tooltip, ToolTipStyle);
            }
        }

        public static bool DisplayingWindow
        {
            get => CursorSettings.DisplayingWindow;
            set { CursorSettings.DisplayingWindow = value; }
        }

        public static Rect AdjustmentRect = new Rect(500, 50, 600f, 500f);

        public static Rect OpenTabRect = new Rect(0, 0, MainWindow.width, 1000f);

        private static Texture2D DragBackgroundTexture => TexturesClass.GetTexture(ColorNames.MidGray);
    }
}